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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JeeBeginner.Services.Encryption;

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
        private readonly IEncryptionService _encryptionService;

        public NhanVienManagementController(
            INhanVienManagementService service,
            IConfiguration configuration,
            IEncryptionService encryptionService)
        {
            _service = service;
            _jwtSecret = configuration.GetValue<string>("JWT:Secret");
            _encryptionService = encryptionService;
        }

        private string ToSearchHashSqlLiteral(string value)
        {
            string hash = _encryptionService.HashSearchIndex(value);
            if (string.IsNullOrWhiteSpace(hash)) return "NULL";
            return "0x" + BitConverter.ToString(Encoding.UTF8.GetBytes(hash)).Replace("-", "");
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

                if (!string.IsNullOrWhiteSpace(daKhoa)) where += " AND Status = 0";
                if (!string.IsNullOrWhiteSpace(dangSuDung)) where += " AND Status = 1";

                int total = 0;
                string activeWhere = where;

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    string k = keyword.Replace("'", "''");
                   
                    k = k.Replace("Đ", "D").Replace("đ", "d");
                    
                    string exactHash = ToSearchHashSqlLiteral(keyword);
                    string fullNameHash = "(1 = 0)";
                    string[] nameParts = keyword.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (nameParts.Length > 1)
                    {
                        string hoLot = string.Join(" ", nameParts.Take(nameParts.Length - 1));
                        string ten = nameParts[nameParts.Length - 1];
                        string hashHoLot = ToSearchHashSqlLiteral(hoLot);
                        string hashTen = ToSearchHashSqlLiteral(ten);
                        string hoLotLike = hoLot.Replace("'", "''");
                        // Khớp chính xác trọn bộ (I_Holot = ... AND I_Ten = ...) cho người gõ đủ 
                        // hoặc Index Seek theo Tên (I_Ten = '') kèm lọc phần lót cho người gõ thiếu họ
                        fullNameHash = $@"((I_Holot = {hashHoLot} AND I_Ten = {hashTen}) OR (I_Ten = {hashTen} AND Holot LIKE N'%{hoLotLike}%'))";
                    }

                    //Phân loại từ khóa để ép SQL Server dùng đúng Index Seek
                    string whereHash;
                    string kwTrim = keyword.Trim();
                    bool isDigitsOnly = System.Text.RegularExpressions.Regex.IsMatch(kwTrim, @"^\d+$");
                    bool isMaNV = System.Text.RegularExpressions.Regex.IsMatch(kwTrim, @"^NV\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    if (isDigitsOnly)
                    {
                        //    Nếu 9 hoặc 12 số -> CCCD đầy đủ (Hash Index Seek tuyệt đối)
                        //    Nếu gõ vài số đầu (Ví dụ "033" hoặc "033701") -> Prefix Range Index Seek trên CMND 
                        //    Trường hợp khác -> Hash Index Seek Sotaikhoan
                        if (kwTrim.Length == 9 || kwTrim.Length == 12)
                            whereHash = where + $@" AND I_CMND = {exactHash}";
                        else if (kwTrim.Length < 9)
                        {
                            string prefix = kwTrim.Replace("'", "''");
                            whereHash = where + $@" AND (CMND LIKE N'{prefix}%' OR Mobile LIKE N'{prefix}%')";
                        }
                        else
                            whereHash = where + $@" AND I_Sotaikhoan = {exactHash}";
                    }
                    else if (isMaNV)
                    {
                        // Mã NV -> Exact Hash HOẶC Prefix Index Seek (MaNV LIKE 'NV10%')
                        string prefix = kwTrim.Replace("'", "''");
                        whereHash = where + $@" AND (I_MaNV = {exactHash} OR MaNV LIKE N'{prefix}%')";
                    }
                    else
                    {
                        // Tên / Họ tên -> Exact Hash HOẶC Prefix Index Seek (Ten/Holot LIKE 'Nguy%')
                        string prefix = kwTrim.Replace("'", "''");
                        if (nameParts.Length > 1)
                            whereHash = where + $@" AND ({fullNameHash} OR Holot LIKE N'{prefix}%' OR Ten LIKE N'{prefix}%')";
                        else
                            whereHash = where + $@" AND (I_Ten = {exactHash} OR Ten LIKE N'{prefix}%' OR Holot LIKE N'{prefix}%')";
                    }

                    total = await _service.CountNhanVien(whereHash);
                    activeWhere = whereHash;

                    
                    // if (total == 0)
                    // {
                    //     string whereLike = where + $@" AND (
                    //         {plainMatch}
                    //     )";
                    //     total = await _service.CountNhanVien(whereLike);
                    //     activeWhere = whereLike;
                    // }
                }
                else
                {
                    total = await _service.CountNhanVien(where);
                }

                PageModel page = new PageModel { TotalCount = total, AllPage = (int)Math.Ceiling(total / (decimal)query.record), Size = query.record, Page = query.page };
                IEnumerable<NhanVienModel> items = total == 0
                    ? Enumerable.Empty<NhanVienModel>()
                    : await _service.Get_DSNhanVien(activeWhere, "Id_NV DESC", query.page, query.record);
                return Ok(JsonResultCommon.ThanhCong(items, page));
            }
            catch (Exception ex) { return BadRequest(JsonResultCommon.Exception(ex)); }
        }

        [HttpGet("GetNhanVienById")]
        public async Task<object> GetNhanVienById(int id) { try { NhanVienModel data = await _service.GetNhanVienById(id); return data is null ? JsonResultCommon.KhongTonTai(id.ToString()) : JsonResultCommon.ThanhCong(data); } catch (Exception ex) { return JsonResultCommon.Exception(ex); } }

        [HttpPost("RebuildSearchIndexes")]
        public async Task<object> RebuildSearchIndexes([FromQuery] int batchSize = 1000)
        {
            try { return JsonResultCommon.ThanhCong(await _service.RebuildSearchIndexes(batchSize)); }
            catch (Exception ex) { return JsonResultCommon.Exception(ex); }
        }

        [HttpPost("CreateNhanVien")]
        public async Task<object> CreateNhanVien([FromBody] NhanVienModel model) { try { string validationError = ValidateNhanVien(model, false); if (validationError != null) return JsonResultCommon.Custom(validationError); ReturnSqlModel result = await _service.CreateNhanVien(model); return result.Susscess ? JsonResultCommon.ThanhCong(model) : JsonResultCommon.ThatBai(result.ErrorMessgage); } catch (Exception ex) { return JsonResultCommon.Exception(ex); } }

        [HttpPost("ImportNhanVien")]
        public async Task<object> ImportNhanVien([FromBody] NhanVienModel model)
        {
            try
            {
                string validationError = ValidateNhanVien(model, false);
                if (validationError != null) return JsonResultCommon.Custom(validationError);
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
                    string validationError = ValidateNhanVien(model, false);
                    if (validationError != null)
                    {
                        errors.Add(new { row = rowNumber, message = validationError });
                        continue;
                    }
                    ReturnSqlModel result = await _service.CreateNhanVien(model);
                    if (result.Susscess) success++;
                    else errors.Add(new { row = rowNumber, message = result.ErrorMessgage });
                }

                return Ok(JsonResultCommon.ThanhCong(new { success, failed = errors.Count, errors }));
            }
            catch (Exception ex) { return BadRequest(JsonResultCommon.Exception(ex)); }
        }

        [HttpPost("UpdateNhanVien")]
        public async Task<object> UpdateNhanVien([FromBody] NhanVienModel model) { try { string validationError = ValidateNhanVien(model, true); if (validationError != null) return JsonResultCommon.Custom(validationError); ReturnSqlModel result = await _service.UpdateNhanVien(model); return result.Susscess ? JsonResultCommon.ThanhCong(model) : JsonResultCommon.ThatBai(result.ErrorMessgage); } catch (Exception ex) { return JsonResultCommon.Exception(ex); } }
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

        private static string ValidateNhanVien(NhanVienModel model, bool isUpdate)
        {
            if (model == null) return "Dữ liệu nhân viên là bắt buộc";
            if (isUpdate && model.Id <= 0) return "Id nhân viên không hợp lệ";

            model.MaNV = model.MaNV?.Trim().ToUpperInvariant();
            model.HoTen = Regex.Replace(model.HoTen ?? string.Empty, @"\s+", " ").Trim();
            model.SDT = model.SDT?.Trim();
            model.CCCD = model.CCCD?.Trim();
            model.Email = model.Email?.Trim();
            model.DiaChi = model.DiaChi?.Trim();
            model.PhongBan = model.PhongBan?.Trim();
            model.ChucVu = model.ChucVu?.Trim();

            if (string.IsNullOrWhiteSpace(model.MaNV)) return "Mã nhân viên là bắt buộc";
            if (!Regex.IsMatch(model.MaNV, "^NV\\d{1,10}$", RegexOptions.IgnoreCase)) return "Mã nhân viên phải có tiền tố NV, theo sau là chữ số (ví dụ: NV105)";
            if (model.HoTen.Length < 2 || model.HoTen.Length > 100) return "Họ tên phải từ 2 đến 100 ký tự";
            if (string.IsNullOrWhiteSpace(model.CCCD)) return "CCCD là bắt buộc";
            if (!Regex.IsMatch(model.CCCD, "^(\\d{9}|\\d{12})$")) return "CCCD phải gồm đúng 12 chữ số hoặc CMND cũ gồm đúng 9 chữ số";
            if (!string.IsNullOrWhiteSpace(model.SDT) && !Regex.IsMatch(model.SDT, "^0\\d{9}$")) return "Số điện thoại phải gồm đúng 10 chữ số và bắt đầu bằng 0";
            if (!string.IsNullOrWhiteSpace(model.Email) && (!Regex.IsMatch(model.Email, "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$") || model.Email.Length > 100)) return "Email không đúng định dạng";
            if (model.DiaChi?.Length > 255) return "Địa chỉ không được quá 255 ký tự";
            if (!string.IsNullOrWhiteSpace(model.PhongBan) && (!decimal.TryParse(model.PhongBan, out decimal departmentId) || departmentId <= 0)) return "Phòng ban phải là mã số dương";
            if (model.ChucVu?.Length > 100) return "Chức vụ không được quá 100 ký tự";

            return null;
        }
        [HttpGet("search")]
        public async Task<object> Search([FromQuery] string keyword)
        {
            try
            {
                var result = await _service.SearchNhanVien(keyword);

                if (result == null)
                    return JsonResultCommon.KhongTonTai("Dữ liệu");

                return JsonResultCommon.ThanhCong(result);
            }
            catch (Exception ex)
            {
                return JsonResultCommon.Exception(ex);
            }
        }
    }
}
