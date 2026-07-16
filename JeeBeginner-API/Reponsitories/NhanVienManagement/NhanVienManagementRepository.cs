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
            ISNULL(CMND, '') AS CCCD, ISNULL(Email, '') AS Email,
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
                DataTable ids = await cnn.CreateDataTableAsync($"SELECT ISNULL(MAX(CAST(Id_NV AS INT)), 0) + 1 AS NextId FROM dbo.{TableName}");
                int nextId = ids.Rows.Count == 0 ? 1 : Convert.ToInt32(ids.Rows[0]["NextId"]);
                SplitHoTen(model.HoTen, out string hoLot, out string ten);
                Hashtable values = Values(model, hoLot, ten, true);
                AddEncryptedValues(values, hoLot, ten, model.CCCD);
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
                SqlConditions conditions = new SqlConditions();
                conditions.Add("Id_NV", model.Id);
                Hashtable values = Values(model, hoLot, ten, false);
                AddEncryptedValues(values, hoLot, ten, model.CCCD);
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
            DataTable rows = await cnn.CreateDataTableAsync(@"SELECT Id_NV, Holot, Ten, CMND
                FROM dbo.Tbl_Nhanvien
                WHERE Holot_Enc IS NULL OR Ten_Enc IS NULL OR CMND_Enc IS NULL OR CMNDHash IS NULL");

            int updated = 0;
            foreach (DataRow row in rows.Rows)
            {
                NhanVienCryptoModel encrypted = _encryptionService.EncryptNhanVienAes(new NhanVienCryptoModel
                {
                    I_Holot = row["Holot"] == DBNull.Value ? null : Convert.ToString(row["Holot"]),
                    I_Ten = row["Ten"] == DBNull.Value ? null : Convert.ToString(row["Ten"]),
                    I_CMND = row["CMND"] == DBNull.Value ? null : Convert.ToString(row["CMND"])
                });

                Hashtable values = new Hashtable
                {
                    { "Holot_Enc", encrypted.Holot_Enc },
                    { "Ten_Enc", encrypted.Ten_Enc },
                    { "CMND_Enc", encrypted.CMND_Enc },
                    { "CMNDHash", encrypted.CMNDHash },
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
            Hashtable values = new Hashtable { { "MaNV", model.MaNV }, { "Holot", hoLot }, { "Ten", ten }, { "Mobile", model.SDT }, { "CMND", model.CCCD }, { "Email", model.Email }, { "Thuongtru_diachi", model.DiaChi }, { "Id_bp", ParseNullableDecimal(model.PhongBan) }, { "Tenchucvu", model.ChucVu }, { "LastModified", DateTime.Now } };
            if (isNew) { values.Add("Status", 1); values.Add("Disable", false); values.Add("DateCreated", DateTime.Now); }
            return values;
        }

        private void AddEncryptedValues(Hashtable values, string hoLot, string ten, string cccd)
        {
            NhanVienCryptoModel encrypted = _encryptionService.EncryptNhanVienAes(new NhanVienCryptoModel
            {
                I_Holot = hoLot,
                I_Ten = ten,
                I_CMND = cccd
            });

            values.Add("Holot_Enc", encrypted.Holot_Enc);
            values.Add("Ten_Enc", encrypted.Ten_Enc);
            values.Add("CMND_Enc", encrypted.CMND_Enc);
            values.Add("CMNDHash", encrypted.CMNDHash);
        }

        private static NhanVienModel MapNhanVien(DataRow r) => new NhanVienModel { Id = r["Id"] == DBNull.Value ? 0 : Convert.ToInt32(r["Id"]), MaNV = Convert.ToString(r["MaNV"]), HoTen = Convert.ToString(r["HoTen"]), SDT = Convert.ToString(r["SDT"]), CCCD = Convert.ToString(r["CCCD"]), Email = Convert.ToString(r["Email"]), DiaChi = Convert.ToString(r["DiaChi"]), PhongBan = Convert.ToString(r["PhongBan"]), ChucVu = Convert.ToString(r["ChucVu"]), Status = r["Status"] == DBNull.Value ? 1 : Convert.ToInt32(r["Status"]), CreatedDate = r["CreatedDate"] == DBNull.Value ? string.Empty : Convert.ToDateTime(r["CreatedDate"]).ToString("dd/MM/yyyy HH:mm:ss") };
        private static void SplitHoTen(string value, out string hoLot, out string ten) { value = (value ?? string.Empty).Trim(); int i = value.LastIndexOf(' '); hoLot = i <= 0 ? string.Empty : value.Substring(0, i).Trim(); ten = i <= 0 ? value : value.Substring(i + 1).Trim(); }
        private static object ParseNullableDecimal(string value) => decimal.TryParse(value, out decimal result) ? (object)result : DBNull.Value;
    }
}
