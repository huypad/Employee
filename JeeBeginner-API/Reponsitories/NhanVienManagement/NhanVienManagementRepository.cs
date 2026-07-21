using DpsLibs.Data;
using JeeBeginner.Models.Common;
using JeeBeginner.Models.NhanVienManagement;
using JeeBeginner.Models.Encryption;
using JeeBeginner.Services.Encryption;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Text;

namespace JeeBeginner.Reponsitories.NhanVienManagement
{
    public class NhanVienManagementRepository : INhanVienManagementRepository
    {
        private const string TableName = "Tbl_Nhanvien";
        private readonly string _connectionString;
        private readonly IEncryptionService _encryptionService;

        public NhanVienManagementRepository(IConfiguration configuration, IEncryptionService encryptionService)
        {
            _connectionString = configuration.GetConnectionString("NhanVienConnection")
                ?? configuration.GetConnectionString("DefaultConnection");
            _encryptionService = encryptionService;
        }

        private const string SelectColumns = @"SELECT
            CAST(Id_NV AS INT) AS Id, ISNULL(MaNV, '') AS MaNV,
            LTRIM(RTRIM(CONCAT(ISNULL(Holot, ''), ' ', ISNULL(Ten, '')))) AS HoTen,
            ISNULL(NULLIF(Mobile, ''), ISNULL(Thuongtru_Phone, '')) AS SDT,
            ISNULL(CMND, '') AS CCCD, ISNULL(Sotaikhoan, '') AS SoTaiKhoan, ISNULL(Email, '') AS Email,
            ISNULL(NULLIF(Thuongtru_diachi, ''), ISNULL(Tamtru_diachi, '')) AS DiaChi,
            ISNULL(CONVERT(NVARCHAR(50), Id_bp), '') AS PhongBan,
            ISNULL(NULLIF(Tenchucvu, ''), CONVERT(NVARCHAR(50), Chucvu)) AS ChucVu,
            ISNULL(CONVERT(INT, Status), CASE WHEN ISNULL(Disable, 0) = 1 THEN 0 ELSE 1 END) AS Status,
            DateCreated AS CreatedDate FROM dbo.Tbl_Nhanvien";

        public async Task<IEnumerable<NhanVienModel>> Get_DSNhanVien(string whereStr, string orderByStr)
        {
            string where = string.IsNullOrWhiteSpace(whereStr) ? "1 = 1" : whereStr;
            using DpsConnection cnn = new DpsConnection(_connectionString);
            DataTable dt = await cnn.CreateDataTableAsync($"{SelectColumns} WHERE {where} ORDER BY TRY_CONVERT(INT, REPLACE(MaNV, 'NV', '')), Id_NV");
            return dt.AsEnumerable().Select(MapNhanVien).ToList();
        }

        public async Task<NhanVienModel> GetNhanVienById(int id)
        {
            using DpsConnection cnn = new DpsConnection(_connectionString);
            DataTable dt = await cnn.CreateDataTableAsync($"{SelectColumns} WHERE Id_NV = {id}");
            return dt.AsEnumerable().Select(MapNhanVien).SingleOrDefault();
        }

        public async Task<ReturnSqlModel> CreateNhanVien(NhanVienModel model)
        {
            try
            {
                using DpsConnection cnn = new DpsConnection(_connectionString);
                string maNhanVien = (model.MaNV ?? string.Empty).Replace("'", "''");
                DataTable duplicate = await cnn.CreateDataTableAsync($"SELECT TOP 1 Id_NV FROM dbo.{TableName} WHERE MaNV = N'{maNhanVien}'");
                if (duplicate.Rows.Count > 0) return new ReturnSqlModel("Mã nhân viên đã tồn tại", "0");
                string cccd = (model.CCCD ?? string.Empty).Replace("'", "''");
                DataTable duplicateCccd = await cnn.CreateDataTableAsync($"SELECT TOP 1 Id_NV FROM dbo.{TableName} WHERE CMND = N'{cccd}'");
                if (duplicateCccd.Rows.Count > 0) return new ReturnSqlModel("CCCD đã tồn tại", "0");
                DataTable ids = await cnn.CreateDataTableAsync($"SELECT ISNULL(MAX(CAST(Id_NV AS INT)), 0) + 1 AS NextId FROM dbo.{TableName}");
                int nextId = ids.Rows.Count == 0 ? 1 : Convert.ToInt32(ids.Rows[0]["NextId"]);
                SplitHoTen(model.HoTen, out string hoLot, out string ten);
                Hashtable values = Values(model, hoLot, ten, true);
                AddEncryptedValues(values, hoLot, ten, model.CCCD, model.SoTaiKhoan);
                values.Add("Id_NV", nextId);
                int result = cnn.Insert(values, TableName);
                if (result <= 0) return new ReturnSqlModel(cnn.LastError.ToString(), "0");
                model.Id = nextId;
                return new ReturnSqlModel();
            }
            catch (Exception ex) { return new ReturnSqlModel(ex.Message, "0"); }
        }

        public async Task<ReturnSqlModel> UpdateNhanVien(NhanVienModel model)
        {
            try
            {
                using DpsConnection cnn = new DpsConnection(_connectionString);
                SplitHoTen(model.HoTen, out string hoLot, out string ten);
                string cccd = (model.CCCD ?? string.Empty).Replace("'", "''");
                DataTable duplicateCccd = await cnn.CreateDataTableAsync($"SELECT TOP 1 Id_NV FROM dbo.{TableName} WHERE CMND = N'{cccd}' AND Id_NV <> {model.Id}");
                if (duplicateCccd.Rows.Count > 0) return new ReturnSqlModel("CCCD đã tồn tại", "0");
                SqlConditions conditions = new SqlConditions();
                conditions.Add("Id_NV", model.Id);
                Hashtable values = Values(model, hoLot, ten, false);
                AddEncryptedValues(values, hoLot, ten, model.CCCD, model.SoTaiKhoan);
                int result = cnn.Update(values, conditions, TableName);
                return result <= 0 ? new ReturnSqlModel(cnn.LastError.ToString(), "0") : new ReturnSqlModel();
            }
            catch (Exception ex) { return new ReturnSqlModel(ex.Message, "0"); }
        }

        public async Task<ReturnSqlModel> DeleteNhanVien(int id)
        {
            try { using DpsConnection cnn = new DpsConnection(_connectionString); SqlConditions c = new SqlConditions(); c.Add("Id_NV", id); return cnn.Delete(c, TableName) <= 0 ? new ReturnSqlModel(cnn.LastError.ToString(), "0") : new ReturnSqlModel(); }
            catch (Exception ex) { return new ReturnSqlModel(ex.Message, "0"); }
        }

        public Task<ReturnSqlModel> UpdateLock(int id) => UpdateStatus(id, 0);
        public Task<ReturnSqlModel> UpdateUnLock(int id) => UpdateStatus(id, 1);

        public async Task<int> EncryptExistingNhanViens()
        {
            using DpsConnection cnn = new DpsConnection(_connectionString);
            DataTable rows = await cnn.CreateDataTableAsync(@"SELECT Id_NV, Holot, Ten, CMND, Sotaikhoan
        FROM dbo.Tbl_Nhanvien
        WHERE Holot_Enc IS NULL OR Ten_Enc IS NULL OR CMND_Enc IS NULL OR CMND_FPE IS NULL OR CMNDHash IS NULL
            OR CMND_Enc NOT LIKE 'RSAHYBRID:%'
            OR I_Holot IS NULL OR I_Ten IS NULL OR I_CMND IS NULL OR I_Sotaikhoan IS NULL");

            int updated = 0;

            // Hàm hỗ trợ ép byte cho gọn code
            object GetBytesSafe(string val) => val == null ? DBNull.Value : (object)Encoding.UTF8.GetBytes(val);

            foreach (DataRow row in rows.Rows)
            {
                string holot = row["Holot"] == DBNull.Value ? null : Convert.ToString(row["Holot"]);
                string ten = row["Ten"] == DBNull.Value ? null : Convert.ToString(row["Ten"]);
                string cmnd = row["CMND"] == DBNull.Value ? null : Convert.ToString(row["CMND"]);
                string sotaikhoan = row["Sotaikhoan"] == DBNull.Value ? null : Convert.ToString(row["Sotaikhoan"]);

                NhanVienCryptoModel encrypted = _encryptionService.EncryptNhanVienWithRsaAndFpeCccd(new NhanVienCryptoModel
                {
                    I_Holot = holot,
                    I_Ten = ten,
                    I_CMND = cmnd,
                    I_Sotaikhoan = sotaikhoan
                });

                Hashtable values = new Hashtable
        {
            // Các cột NVARCHAR
            { "Holot_Enc", encrypted.Holot_Enc ?? (object)DBNull.Value },
            { "Ten_Enc", encrypted.Ten_Enc ?? (object)DBNull.Value },
            { "CMND_Enc", encrypted.CMND_Enc ?? (object)DBNull.Value },
            { "CMND_FPE", encrypted.CMND_FPE ?? (object)DBNull.Value },
            { "CMNDHash", encrypted.CMNDHash ?? (object)DBNull.Value },
            { "SotaikhoanHash", encrypted.SotaikhoanHash ?? (object)DBNull.Value },

            // Các cột VARBINARY
            { "I_Holot", GetBytesSafe(_encryptionService.HashSearchIndex(holot)) },
            { "I_Ten", GetBytesSafe(_encryptionService.HashSearchIndex(ten)) },
            { "I_CMND", GetBytesSafe(_encryptionService.HashSearchIndex(cmnd)) },
            { "I_Sotaikhoan", GetBytesSafe(_encryptionService.HashSearchIndex(sotaikhoan)) },

            { "LastModified", DateTime.Now }
        };

                SqlConditions conditions = new SqlConditions();
                conditions.Add("Id_NV", Convert.ToInt32(row["Id_NV"]));
                if (cnn.Update(values, conditions, TableName) > 0) updated++;
            }

            return updated;
        }

        private async Task<ReturnSqlModel> UpdateStatus(int id, int status)
        {
            try { using DpsConnection cnn = new DpsConnection(_connectionString); Hashtable v = new Hashtable { { "Status", status }, { "Disable", status == 0 }, { "LastModified", DateTime.Now } }; SqlConditions c = new SqlConditions(); c.Add("Id_NV", id); return cnn.Update(v, c, TableName) <= 0 ? new ReturnSqlModel(cnn.LastError.ToString(), "0") : new ReturnSqlModel(); }
            catch (Exception ex) { return new ReturnSqlModel(ex.Message, "0"); }
        }

        private static Hashtable Values(NhanVienModel model, string hoLot, string ten, bool isNew)
        {
            Hashtable values = new Hashtable { { "MaNV", model.MaNV }, { "Holot", hoLot }, { "Ten", ten }, { "Mobile", model.SDT }, { "CMND", model.CCCD }, { "Sotaikhoan", string.IsNullOrWhiteSpace(model.SoTaiKhoan) ? (object)DBNull.Value : model.SoTaiKhoan }, { "Email", model.Email }, { "Thuongtru_diachi", model.DiaChi }, { "Id_bp", ParseNullableDecimal(model.PhongBan) }, { "Tenchucvu", model.ChucVu }, { "LastModified", DateTime.Now } };
            if (isNew) { values.Add("Status", 1); values.Add("Disable", false); values.Add("DateCreated", DateTime.Now); }
            return values;
        }

        private void AddEncryptedValues(Hashtable values, string hoLot, string ten, string cccd, string soTaiKhoan)
        {
            NhanVienCryptoModel encrypted = _encryptionService.EncryptNhanVienWithRsaAndFpeCccd(new NhanVienCryptoModel
            {
                I_Holot = hoLot,
                I_Ten = ten,
                I_CMND = cccd,
                I_Sotaikhoan = soTaiKhoan
            });

            // Hàm hỗ trợ gán cột NVARCHAR (Chuỗi bình thường)
            void AddString(string key, string val) => values.Add(key, val == null ? DBNull.Value : (object)val);

            // Hàm hỗ trợ gán cột VARBINARY (Ép chuỗi thành mảng byte)
            void AddVarbinary(string key, string val) => values.Add(key, val == null ? DBNull.Value : (object)Encoding.UTF8.GetBytes(val));

            // 1. Các cột kiểu NVARCHAR
            AddString("Holot_Enc", encrypted.Holot_Enc);
            AddString("Ten_Enc", encrypted.Ten_Enc);
            AddString("CMND_Enc", encrypted.CMND_Enc);
            AddString("CMND_FPE", encrypted.CMND_FPE);
            AddString("CMNDHash", encrypted.CMNDHash);
            AddString("SotaikhoanHash", encrypted.SotaikhoanHash);

            // 2. Các cột kiểu VARBINARY (Phải ép sang mảng byte)
            AddVarbinary("I_Holot", _encryptionService.HashSearchIndex(hoLot));
            AddVarbinary("I_Ten", _encryptionService.HashSearchIndex(ten));
            AddVarbinary("I_CMND", _encryptionService.HashSearchIndex(cccd));
            AddVarbinary("I_Sotaikhoan", _encryptionService.HashSearchIndex(soTaiKhoan));
        }

        private static NhanVienModel MapNhanVien(DataRow r) => new NhanVienModel { Id = r["Id"] == DBNull.Value ? 0 : Convert.ToInt32(r["Id"]), MaNV = Convert.ToString(r["MaNV"]), HoTen = Convert.ToString(r["HoTen"]), SDT = Convert.ToString(r["SDT"]), CCCD = Convert.ToString(r["CCCD"]), SoTaiKhoan = Convert.ToString(r["SoTaiKhoan"]), Email = Convert.ToString(r["Email"]), DiaChi = Convert.ToString(r["DiaChi"]), PhongBan = Convert.ToString(r["PhongBan"]), ChucVu = Convert.ToString(r["ChucVu"]), Status = r["Status"] == DBNull.Value ? 1 : Convert.ToInt32(r["Status"]), CreatedDate = r["CreatedDate"] == DBNull.Value ? string.Empty : Convert.ToDateTime(r["CreatedDate"]).ToString("dd/MM/yyyy HH:mm:ss") };
        private static void SplitHoTen(string value, out string hoLot, out string ten) { value = (value ?? string.Empty).Trim(); int i = value.LastIndexOf(' '); hoLot = i <= 0 ? string.Empty : value.Substring(0, i).Trim(); ten = i <= 0 ? value : value.Substring(i + 1).Trim(); }
        private static object ParseNullableDecimal(string value) => decimal.TryParse(value, out decimal result) ? (object)result : DBNull.Value;
        public async Task<IEnumerable<NhanVienModel>> SearchAllEncrypted(string plainKeyword, string hashedKeyword)
        {
            string plainSafe = (plainKeyword ?? string.Empty).Replace("'", "''");
            string hashSafe = (hashedKeyword ?? string.Empty).Replace("'", "''");

          
            byte[] hashBytes = string.IsNullOrEmpty(hashSafe) ? new byte[0] : Encoding.UTF8.GetBytes(hashSafe);
            string hexHash = hashBytes.Length > 0 ? "0x" + BitConverter.ToString(hashBytes).Replace("-", "") : "NULL";

            string query = $@"{SelectColumns} 
WHERE 
    -- 1. TÌM KIẾM TRÊN CÁC CỘT MÃ HÓA (Đã tối ưu mã Hexa, Database sẽ ăn Index rất sâu)
    (I_Holot = {hexHash}
     OR I_Ten = {hexHash}
     OR I_CMND = {hexHash}
     OR I_Sotaikhoan = {hexHash}
     -- Hai cột này DB đang lưu kiểu NVARCHAR, vẫn dùng chuỗi bình thường
     OR SotaikhoanHash = N'{hashSafe}'
     OR CMNDHash = N'{hashSafe}')
     
    -- 2. TÌM KIẾM TƯƠNG ĐỐI (LIKE) TRÊN CÁC CỘT MÃ RÕ
    OR (MaNV LIKE N'%{plainSafe}%' 
        OR Holot LIKE N'%{plainSafe}%' 
        OR Ten LIKE N'%{plainSafe}%' 
        OR CMND LIKE N'%{plainSafe}%' 
        OR Mobile LIKE N'%{plainSafe}%' 
        OR Email LIKE N'%{plainSafe}%')
ORDER BY TRY_CONVERT(INT, REPLACE(MaNV, 'NV', '')), Id_NV DESC";

            using DpsConnection cnn = new DpsConnection(_connectionString);
            DataTable dt = await cnn.CreateDataTableAsync(query);

            return dt.AsEnumerable().Select(MapNhanVien).ToList();
        }
    }
}
