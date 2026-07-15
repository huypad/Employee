using JeeBeginner.Classes;
using JeeBeginner.Models.Common;
using JeeBeginner.Models.NhanVienManagement;
using JeeBeginner.Services.NhanVienManagement;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JeeBeginner.Controllers
{
    [EnableCors("AllowOrigin")]
    [Route("api/nhanvienmanagement")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class NhanVienManagementController : ControllerBase
    {
        private readonly INhanVienManagementService _service;
        private readonly string _jwtSecret;
        public NhanVienManagementController(INhanVienManagementService service, IConfiguration configuration)
        {
            _service = service;
            _jwtSecret = configuration.GetValue<string>("JWT:Secret");
        }

        [HttpGet("Get_DSNhanVien")]
        public async Task<ActionResult> Get_DSNhanVien([FromQuery] QueryParams query)
        {
            try
            {
                query ??= new QueryParams();
                if (query.page <= 0) query.page = 1;
                if (query.record <= 0) query.record = 10;
                string where = "1 = 1";
                query.filter ??= new FilterModel();
                string keyword = query.filter["keyword"];
                string daKhoa = query.filter["dakhoa"];
                string dangSuDung = query.filter["dangsudung"];

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    string k = keyword.Replace("'", "''");
                    where += $@" AND (MaNV LIKE N'%{k}%' OR Holot LIKE N'%{k}%' OR Ten LIKE N'%{k}%' OR Mobile LIKE N'%{k}%' OR CMND LIKE N'%{k}%' OR Email LIKE N'%{k}%')";
                }
                if (!string.IsNullOrWhiteSpace(daKhoa)) where += " AND Status = 0";
                if (!string.IsNullOrWhiteSpace(dangSuDung)) where += " AND Status = 1";
                IEnumerable<NhanVienModel> all = await _service.Get_DSNhanVien(where, "Id_NV DESC") ?? Enumerable.Empty<NhanVienModel>();
                int total = all.Count();
                PageModel page = new PageModel { TotalCount = total, AllPage = (int)Math.Ceiling(total / (decimal)query.record), Size = query.record, Page = query.page };
                if (query.more) { query.page = 1; query.record = total; }
                return Ok(JsonResultCommon.ThanhCong(all.Skip((query.page - 1) * query.record).Take(query.record), page));
            }
            catch (Exception ex) { return BadRequest(JsonResultCommon.Exception(ex)); }
        }

        [HttpGet("GetNhanVienById")]
        public async Task<object> GetNhanVienById(int id) { try { NhanVienModel data = await _service.GetNhanVienById(id); return data is null ? JsonResultCommon.KhongTonTai(id.ToString()) : JsonResultCommon.ThanhCong(data); } catch (Exception ex) { return JsonResultCommon.Exception(ex); } }
        [HttpPost("CreateNhanVien")]
        public async Task<object> CreateNhanVien([FromBody] NhanVienModel model) { try { if (string.IsNullOrWhiteSpace(model?.MaNV)) return JsonResultCommon.BatBuoc("Mã nhân viên"); if (string.IsNullOrWhiteSpace(model.HoTen)) return JsonResultCommon.BatBuoc("Họ tên"); ReturnSqlModel result = await _service.CreateNhanVien(model); return result.Susscess ? JsonResultCommon.ThanhCong(model) : JsonResultCommon.ThatBai(result.ErrorMessgage); } catch (Exception ex) { return JsonResultCommon.Exception(ex); } }
        [HttpPost("ImportNhanVien")]
        public async Task<object> ImportNhanVien([FromBody] NhanVienModel model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model?.MaNV)) return JsonResultCommon.BatBuoc("Mã nhân viên");
                if (string.IsNullOrWhiteSpace(model.HoTen)) return JsonResultCommon.BatBuoc("Họ tên");
                ReturnSqlModel result = await _service.CreateNhanVien(model);
                return result.Susscess ? JsonResultCommon.ThanhCong(model) : JsonResultCommon.ThatBai(result.ErrorMessgage);
            }
            catch (Exception ex) { return JsonResultCommon.Exception(ex); }
        }

        [HttpGet("DownloadNhanVienImportTemplate")]
        public IActionResult DownloadNhanVienImportTemplate()
        {
            using XLWorkbook workbook = new XLWorkbook();
            IXLWorksheet sheet = workbook.Worksheets.Add("NhanVien");
            string[] headers = { "Mã NV", "Họ tên", "SĐT", "CCCD", "Email", "Địa chỉ", "Phòng ban", "Chức vụ" };
            for (int index = 0; index < headers.Length; index++) sheet.Cell(1, index + 1).Value = headers[index];
            sheet.Row(1).Style.Font.Bold = true;
            sheet.Columns().AdjustToContents();
            using MemoryStream stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Mau_import_nhan_vien.xlsx");
        }

        [HttpPost("ImportNhanVienFromExcel")]
        public async Task<IActionResult> ImportNhanVienFromExcel([FromForm] IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(JsonResultCommon.BatBuoc("file Excel"));
                if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(JsonResultCommon.Custom("Chỉ hỗ trợ file .xlsx"));

                using Stream input = file.OpenReadStream();
                using XLWorkbook workbook = new XLWorkbook(input);
                IXLWorksheet sheet = workbook.Worksheets.FirstOrDefault();
                if (sheet == null || sheet.LastRowUsed() == null)
                    return BadRequest(JsonResultCommon.Custom("File Excel không có dữ liệu"));

                int headerRow = FindHeaderRow(sheet);
                if (headerRow == 0)
                    return BadRequest(JsonResultCommon.Custom("File phải có cột Mã NV và Họ tên"));
                Dictionary<string, int> columns = sheet.Row(headerRow).CellsUsed()
                    .ToDictionary(cell => NormalizeHeader(cell.GetString()), cell => cell.Address.ColumnNumber);
                if (!columns.ContainsKey("MANV") || !columns.ContainsKey("HOTEN"))
                    return BadRequest(JsonResultCommon.Custom("File phải có cột Mã NV và Họ tên"));

                List<object> errors = new List<object>();
                HashSet<string> maNhanVienTrongFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int success = 0;
                int lastRow = sheet.LastRowUsed().RowNumber();
                for (int rowNumber = headerRow + 1; rowNumber <= lastRow; rowNumber++)
                {
                    string maNV = GetCell(sheet, rowNumber, columns, "MANV");
                    string hoTen = GetCell(sheet, rowNumber, columns, "HOTEN");
                    if (string.IsNullOrWhiteSpace(maNV) && string.IsNullOrWhiteSpace(hoTen)) continue;
                    if (string.IsNullOrWhiteSpace(maNV) || string.IsNullOrWhiteSpace(hoTen))
                    {
                        errors.Add(new { row = rowNumber, message = "Mã NV và Họ tên là bắt buộc" });
                        continue;
                    }
                    if (!maNhanVienTrongFile.Add(maNV))
                    {
                        errors.Add(new { row = rowNumber, message = "Mã NV bị trùng trong file" });
                        continue;
                    }

                    NhanVienModel model = new NhanVienModel
                    {
                        MaNV = maNV,
                        HoTen = hoTen,
                        SDT = GetCell(sheet, rowNumber, columns, "SDT", "SODIENTHOAI"),
                        CCCD = GetCell(sheet, rowNumber, columns, "CCCD"),
                        Email = GetCell(sheet, rowNumber, columns, "EMAIL"),
                        DiaChi = GetCell(sheet, rowNumber, columns, "DIACHI"),
                        PhongBan = GetCell(sheet, rowNumber, columns, "PHONGBAN"),
                        ChucVu = GetCell(sheet, rowNumber, columns, "CHUCVU")
                    };
                    ReturnSqlModel result = await _service.CreateNhanVien(model);
                    if (result.Susscess) success++;
                    else errors.Add(new { row = rowNumber, message = result.ErrorMessgage });
                }

                return Ok(JsonResultCommon.ThanhCong(new { success, failed = errors.Count, errors }));
            }
            catch (Exception ex) { return BadRequest(JsonResultCommon.Exception(ex)); }
        }

        [HttpPost("UpdateNhanVien")]
        public async Task<object> UpdateNhanVien([FromBody] NhanVienModel model) { try { if (model?.Id <= 0) return JsonResultCommon.BatBuoc("Id"); ReturnSqlModel result = await _service.UpdateNhanVien(model); return result.Susscess ? JsonResultCommon.ThanhCong(model) : JsonResultCommon.ThatBai(result.ErrorMessgage); } catch (Exception ex) { return JsonResultCommon.Exception(ex); } }
        [HttpGet("DeleteNhanVien/{id}")]
        public async Task<object> DeleteNhanVien(int id) { try { ReturnSqlModel result = await _service.DeleteNhanVien(id); return result.Susscess ? JsonResultCommon.ThanhCong(id) : JsonResultCommon.ThatBai(result.ErrorMessgage); } catch (Exception ex) { return JsonResultCommon.Exception(ex); } }
        [HttpGet("UpdateLock/{id}")]
        public async Task<object> UpdateLock(int id) { try { ReturnSqlModel result = await _service.UpdateLock(id); return result.Susscess ? JsonResultCommon.ThanhCong(id) : JsonResultCommon.ThatBai(result.ErrorMessgage); } catch (Exception ex) { return JsonResultCommon.Exception(ex); } }
        [HttpGet("UpdateUnLock/{id}")]
        public async Task<object> UpdateUnLock(int id) { try { ReturnSqlModel result = await _service.UpdateUnLock(id); return result.Susscess ? JsonResultCommon.ThanhCong(id) : JsonResultCommon.ThatBai(result.ErrorMessgage); } catch (Exception ex) { return JsonResultCommon.Exception(ex); } }

        [HttpPost("EncryptExistingNhanViens")]
        public async Task<object> EncryptExistingNhanViens()
        {
            try
            {
                if (Ulities.GetUserByHeader(HttpContext.Request.Headers, _jwtSecret) is null)
                    return Unauthorized(JsonResultCommon.DangNhap());

                int updated = await _service.EncryptExistingNhanViens();
                return JsonResultCommon.ThanhCong(new { updated });
            }
            catch (Exception ex)
            {
                return JsonResultCommon.Exception(ex);
            }
        }

        private static string GetCell(IXLWorksheet sheet, int row, Dictionary<string, int> columns, params string[] names)
        {
            foreach (string name in names)
                if (columns.TryGetValue(name, out int column)) return sheet.Cell(row, column).GetFormattedString().Trim();
            return string.Empty;
        }

        private static int FindHeaderRow(IXLWorksheet sheet)
        {
            int lastHeaderRow = Math.Min(sheet.LastRowUsed().RowNumber(), 10);
            for (int row = 1; row <= lastHeaderRow; row++)
            {
                HashSet<string> headers = sheet.Row(row).CellsUsed()
                    .Select(cell => NormalizeHeader(cell.GetString()))
                    .ToHashSet();
                if (headers.Contains("MANV") && headers.Contains("HOTEN")) return row;
            }
            return 0;
        }

        private static string NormalizeHeader(string value)
        {
            string normalized = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
            StringBuilder result = new StringBuilder();
            foreach (char character in normalized)
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character))
                    result.Append(character);
            return result.ToString().Replace('Đ', 'D').Replace('đ', 'd').ToUpperInvariant();
        }
    }
}
