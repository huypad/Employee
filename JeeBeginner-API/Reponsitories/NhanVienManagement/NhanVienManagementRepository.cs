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
using System.Data.SqlClient;
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
            ISNULL(Mobile, '') AS SDT,
            ISNULL(CMND, '') AS CCCD, ISNULL(Sotaikhoan, '') AS SoTaiKhoan, ISNULL(Email, '') AS Email,
            ISNULL(DiaChi, '') AS DiaChi,
            ISNULL(PhongBan, '') AS PhongBan,
            ISNULL(Tenchucvu, '') AS ChucVu,
            ISNULL(CONVERT(INT, Status), CASE WHEN ISNULL(Disable, 0) = 1 THEN 0 ELSE 1 END) AS Status,
            DateCreated AS CreatedDate FROM dbo.Tbl_Nhanvien";

        public async Task<IEnumerable<NhanVienModel>> Get_DSNhanVien(string whereStr, string orderByStr, int page, int record)
        {
            string where = string.IsNullOrWhiteSpace(whereStr) ? "1 = 1" : whereStr;
            page = Math.Max(1, page);
            record = Math.Max(1, record);
            int offset = (page - 1) * record;
            string sql = $@"{SelectColumns}
                WHERE {where}
                ORDER BY TRY_CONVERT(INT, REPLACE(MaNV, 'NV', '')), Id_NV
                OFFSET {offset} ROWS FETCH NEXT {record} ROWS ONLY";
            DataTable dt = new DataTable();
            using SqlConnection cnn = new SqlConnection(_connectionString);
            using SqlCommand command = new SqlCommand(sql, cnn);
            await cnn.OpenAsync();
            using SqlDataReader reader = await command.ExecuteReaderAsync();
            dt.Load(reader);
            return dt.AsEnumerable().Select(MapNhanVien).ToList();
        }

        public async Task<int> CountNhanVien(string whereStr)
        {
            string where = string.IsNullOrWhiteSpace(whereStr) ? "1 = 1" : whereStr;
            using SqlConnection cnn = new SqlConnection(_connectionString);
            using SqlCommand command = new SqlCommand($"SELECT COUNT(1) FROM dbo.{TableName} WHERE {where}", cnn);
            await cnn.OpenAsync();
            object total = await command.ExecuteScalarAsync();
            return total == null || total == DBNull.Value ? 0 : Convert.ToInt32(total);
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
                NhanVienCryptoModel enc = Encrypt(model.MaNV, hoLot, ten, model.CCCD, model.SoTaiKhoan);
                string sql = RawInsertSql(nextId, model, hoLot, ten, enc);
                cnn.ExecuteNonQuery(sql);
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
                NhanVienCryptoModel enc = Encrypt(model.MaNV, hoLot, ten, model.CCCD, model.SoTaiKhoan);
                string sql = RawUpdateSql(model.Id, model, hoLot, ten, enc);
                cnn.ExecuteNonQuery(sql);
                return new ReturnSqlModel();
            }
            catch (Exception ex) { return new ReturnSqlModel(ex.Message, "0"); }
        }

        private NhanVienCryptoModel Encrypt(string maNV, string hoLot, string ten, string cccd, string soTaiKhoan) =>
            _encryptionService.EncryptNhanVienWithRsaAndFpeCccd(new NhanVienCryptoModel
            { I_MaNV = maNV, I_Holot = hoLot, I_Ten = ten, I_CMND = cccd, I_Sotaikhoan = soTaiKhoan });

        private string RawInsertSql(int id, NhanVienModel m, string hoLot, string ten, NhanVienCryptoModel enc)
        {
            string now = $"'{DateTime.Now:yyyy-MM-dd HH:mm:ss}'";
            return $@"INSERT INTO dbo.{TableName}
(Id_NV,MaNV,Holot,Ten,Mobile,CMND,Sotaikhoan,Email,DiaChi,PhongBan,Tenchucvu,
 LastModified,Status,Disable,DateCreated,
 MaNV_Enc,Holot_Enc,Ten_Enc,CMND_Enc,CMND_FPE,CMNDHash,
 I_MaNV,I_Holot,I_Ten,I_CMND,I_Sotaikhoan,I_Mobile)
VALUES(
 {id},{S(m.MaNV)},{S(hoLot)},{S(ten)},{S(m.SDT)},{S(m.CCCD)},{SN(m.SoTaiKhoan)},{SN(m.Email)},
 {SN(m.DiaChi)},{SN(m.PhongBan)},{SN(m.ChucVu)},
 {now},1,0,{now},
 {S(enc.MaNV_Enc)},{S(enc.Holot_Enc)},{S(enc.Ten_Enc)},{S(enc.CMND_Enc)},{S(enc.CMND_FPE)},{S(enc.CMNDHash)},
 {Hex(m.MaNV)},{Hex(hoLot)},{Hex(ten)},{Hex(m.CCCD)},{Hex(m.SoTaiKhoan)},{Hex(m.SDT)})";
        }

        private string RawUpdateSql(int id, NhanVienModel m, string hoLot, string ten, NhanVienCryptoModel enc)
        {
            string now = $"'{DateTime.Now:yyyy-MM-dd HH:mm:ss}'";
            return $@"UPDATE dbo.{TableName} SET
 MaNV={S(m.MaNV)},Holot={S(hoLot)},Ten={S(ten)},Mobile={S(m.SDT)},CMND={S(m.CCCD)},
 Sotaikhoan={SN(m.SoTaiKhoan)},Email={SN(m.Email)},DiaChi={SN(m.DiaChi)},
 PhongBan={SN(m.PhongBan)},Tenchucvu={SN(m.ChucVu)},LastModified={now},
 MaNV_Enc={S(enc.MaNV_Enc)},Holot_Enc={S(enc.Holot_Enc)},Ten_Enc={S(enc.Ten_Enc)},CMND_Enc={S(enc.CMND_Enc)},
 CMND_FPE={S(enc.CMND_FPE)},CMNDHash={S(enc.CMNDHash)},
 I_MaNV={Hex(m.MaNV)},I_Holot={Hex(hoLot)},I_Ten={Hex(ten)},I_CMND={Hex(m.CCCD)},I_Sotaikhoan={Hex(m.SoTaiKhoan)},I_Mobile={Hex(m.SDT)}
WHERE Id_NV={id}";
        }

        // Helpers xây SQL literal
        private static string S(string val)  => val == null ? "NULL" : $"N'{val.Replace("'", "''")}'";
        private static string SN(string val) => string.IsNullOrWhiteSpace(val) ? "NULL" : $"N'{val.Replace("'", "''")}'";
        private static string Dec(string val) => decimal.TryParse(val, out decimal d) ? d.ToString() : "NULL";
        private string Hex(string val)
        {
            string hash = _encryptionService.HashSearchIndex(val);
            if (hash == null) return "NULL";
            byte[] bytes = Encoding.UTF8.GetBytes(hash);
            return bytes.Length == 0 ? "NULL" : "0x" + BitConverter.ToString(bytes).Replace("-", "");
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
            DataTable rows = await cnn.CreateDataTableAsync(@"SELECT Id_NV, MaNV, Holot, Ten, CMND, Sotaikhoan, Mobile
        FROM dbo.Tbl_Nhanvien
        WHERE MaNV_Enc IS NULL OR I_MaNV IS NULL OR Holot_Enc IS NULL OR Ten_Enc IS NULL OR CMND_Enc IS NULL OR CMND_FPE IS NULL OR CMNDHash IS NULL
            OR CMND_Enc NOT LIKE 'RSAHYBRID:%'
            OR I_Holot IS NULL OR I_Ten IS NULL OR I_CMND IS NULL OR I_Sotaikhoan IS NULL OR I_Mobile IS NULL");

            int updated = 0;

            foreach (DataRow row in rows.Rows)
            {
                string holot = row["Holot"] == DBNull.Value ? null : Convert.ToString(row["Holot"]);
                string maNV = row["MaNV"] == DBNull.Value ? null : Convert.ToString(row["MaNV"]);
                string ten = row["Ten"] == DBNull.Value ? null : Convert.ToString(row["Ten"]);
                string cmnd = row["CMND"] == DBNull.Value ? null : Convert.ToString(row["CMND"]);
                string sotaikhoan = row["Sotaikhoan"] == DBNull.Value ? null : Convert.ToString(row["Sotaikhoan"]);
                string mobile = row["Mobile"] == DBNull.Value ? null : Convert.ToString(row["Mobile"]);
                int id = Convert.ToInt32(row["Id_NV"]);

                NhanVienCryptoModel enc = Encrypt(maNV, holot, ten, cmnd, sotaikhoan);
                string sql = $@"UPDATE dbo.{TableName} SET
                    MaNV_Enc={S(enc.MaNV_Enc)},Holot_Enc={S(enc.Holot_Enc)},Ten_Enc={S(enc.Ten_Enc)},CMND_Enc={S(enc.CMND_Enc)},
                    CMND_FPE={S(enc.CMND_FPE)},CMNDHash={S(enc.CMNDHash)},
                    I_MaNV={Hex(maNV)},I_Holot={Hex(holot)},I_Ten={Hex(ten)},I_CMND={Hex(cmnd)},I_Sotaikhoan={Hex(sotaikhoan)},I_Mobile={Hex(mobile)},
                    LastModified='{DateTime.Now:yyyy-MM-dd HH:mm:ss}'
                WHERE Id_NV={id}";
                cnn.ExecuteNonQuery(sql);
                updated++;
            }

            return updated;
        }

        public async Task<int> RebuildSearchIndexes(int batchSize)
        {
            batchSize = Math.Clamp(batchSize, 1, 5000);
            using DpsConnection cnn = new DpsConnection(_connectionString);
            DataTable rows = await cnn.CreateDataTableAsync($@"SELECT TOP {batchSize} Id_NV, MaNV, Holot, Ten, CMND, Sotaikhoan, Mobile
                FROM dbo.Tbl_Nhanvien
                -- I_MaNV is the progress marker shown to the administrator.
                -- Only take rows that still lack this marker so each completed batch
                -- increases the I_MaNV counter and is not repeatedly reprocessed.
                WHERE I_MaNV IS NULL
                ORDER BY Id_NV");

            int updated = 0;
            foreach (DataRow row in rows.Rows)
            {
                int id = Convert.ToInt32(row["Id_NV"]);
                string maNV = row["MaNV"] == DBNull.Value ? null : Convert.ToString(row["MaNV"]);
                string holot = row["Holot"] == DBNull.Value ? null : Convert.ToString(row["Holot"]);
                string ten = row["Ten"] == DBNull.Value ? null : Convert.ToString(row["Ten"]);
                string cmnd = row["CMND"] == DBNull.Value ? null : Convert.ToString(row["CMND"]);
                string sotaikhoan = row["Sotaikhoan"] == DBNull.Value ? null : Convert.ToString(row["Sotaikhoan"]);
                string mobile = row["Mobile"] == DBNull.Value ? null : Convert.ToString(row["Mobile"]);
                cnn.ExecuteNonQuery($@"UPDATE dbo.{TableName} SET
                    I_MaNV={Hex(maNV)}, I_Holot={Hex(holot)}, I_Ten={Hex(ten)}, I_CMND={Hex(cmnd)}, I_Sotaikhoan={Hex(sotaikhoan)}, I_Mobile={Hex(mobile)},
                    LastModified='{DateTime.Now:yyyy-MM-dd HH:mm:ss}' WHERE Id_NV={id}");
                updated++;
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

            // 1. Các cột kiểu NVARCHAR
            AddString("Holot_Enc", encrypted.Holot_Enc);
            AddString("Ten_Enc", encrypted.Ten_Enc);
            AddString("CMND_Enc", encrypted.CMND_Enc);
            AddString("CMND_FPE", encrypted.CMND_FPE);
            AddString("CMNDHash", encrypted.CMNDHash);

            // 2. Các cột VARBINARY — phải khai báo rõ DBNull để DPS không tự điền NVARCHAR
            // (UpdateBinaryIndexes sẽ ghi đúng giá trị byte sau khi Insert/Update hoàn tất)
            values["I_Holot"]      = DBNull.Value;
            values["I_Ten"]        = DBNull.Value;
            values["I_CMND"]       = DBNull.Value;
            values["I_Sotaikhoan"] = DBNull.Value;
        }


        private static NhanVienModel MapNhanVien(DataRow r) => new NhanVienModel { Id = r["Id"] == DBNull.Value ? 0 : Convert.ToInt32(r["Id"]), MaNV = Convert.ToString(r["MaNV"]), HoTen = Convert.ToString(r["HoTen"]), SDT = Convert.ToString(r["SDT"]), CCCD = Convert.ToString(r["CCCD"]), SoTaiKhoan = Convert.ToString(r["SoTaiKhoan"]), Email = Convert.ToString(r["Email"]), DiaChi = Convert.ToString(r["DiaChi"]), PhongBan = Convert.ToString(r["PhongBan"]), ChucVu = Convert.ToString(r["ChucVu"]), Status = r["Status"] == DBNull.Value ? 1 : Convert.ToInt32(r["Status"]), CreatedDate = r["CreatedDate"] == DBNull.Value ? string.Empty : Convert.ToDateTime(r["CreatedDate"]).ToString("dd/MM/yyyy HH:mm:ss") };
        private static void SplitHoTen(string value, out string hoLot, out string ten) { value = (value ?? string.Empty).Trim(); int i = value.LastIndexOf(' '); hoLot = i <= 0 ? string.Empty : value.Substring(0, i).Trim(); ten = i <= 0 ? value : value.Substring(i + 1).Trim(); }
        private static object ParseNullableDecimal(string value) => decimal.TryParse(value, out decimal result) ? (object)result : DBNull.Value;
        public async Task<IEnumerable<NhanVienModel>> SearchAllEncrypted(string plainKeyword, string hashedKeyword)
        {
            string hashSafe = (hashedKeyword ?? string.Empty).Replace("'", "''");

            byte[] hashBytes = string.IsNullOrEmpty(hashSafe) ? new byte[0] : Encoding.UTF8.GetBytes(hashSafe);
            string hexHash = hashBytes.Length > 0 ? "0x" + BitConverter.ToString(hashBytes).Replace("-", "") : "NULL";

            string query = $@"{SelectColumns} 
WHERE 
    -- 100% TÌM KIẾM TUYỆT ĐỐI TRÊN CÁC CỘT CHỈ MỤC (VARBINARY(32) - INDEX SEEK)
    I_MaNV = {hexHash}
    OR I_Holot = {hexHash}
    OR I_Ten = {hexHash}
    OR I_CMND = {hexHash}
    OR I_Sotaikhoan = {hexHash}
    OR I_Mobile = {hexHash}
ORDER BY TRY_CONVERT(INT, REPLACE(MaNV, 'NV', '')), Id_NV DESC";

            using DpsConnection cnn = new DpsConnection(_connectionString);
            DataTable dt = await cnn.CreateDataTableAsync(query);

            return dt.AsEnumerable().Select(MapNhanVien).ToList();
        }
    }
}
