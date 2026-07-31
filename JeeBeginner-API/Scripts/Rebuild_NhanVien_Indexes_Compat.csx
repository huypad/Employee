#!/usr/bin/env dotnet-script
#r "nuget: Microsoft.Data.SqlClient, 5.2.1"

// Compatible version for older dotnet-script. Run from JeeBeginner-API.
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

const int DefaultBatchSize = 1000;
string projectDirectory = Directory.GetCurrentDirectory();
Dictionary<string, string> envFile = LoadDotEnv(Path.Combine(projectDirectory, ".env"));
string connectionString = GetSetting("ConnectionStrings__DefaultConnection");
string hmacKeyBase64 = GetSetting("Encryption__HmacKey");
byte[] hmacKey = Convert.FromBase64String(hmacKeyBase64);
int batchSize = GetBatchSize();

if (hmacKey.Length != 32)
    throw new InvalidOperationException("Encryption__HmacKey phai giai ma Base64 thanh 32 byte.");

Console.WriteLine("=== Rebuild employee blind indexes ===");
Console.WriteLine("Batch size: " + batchSize);

using (SqlConnection connection = new SqlConnection(connectionString))
{
    connection.Open();
    EnsureMobileIndexSchema(connection);

    int totalUpdated = 0;
    int lastId = 0;
    while (true)
    {
        List<EmployeeRow> rows = ReadBatch(connection, lastId, batchSize);
        if (rows.Count == 0) break;

        using (SqlTransaction transaction = connection.BeginTransaction())
        using (SqlCommand update = new SqlCommand(@"
            UPDATE dbo.Tbl_Nhanvien SET
                I_MaNV = @maNV,
                I_Holot = @holot,
                I_Ten = @ten,
                I_CMND = @cmnd,
                I_Sotaikhoan = @sotaikhoan,
                I_Mobile = @mobile
            WHERE Id_NV = @id;", connection, transaction))
        {
            update.Parameters.Add("@maNV", System.Data.SqlDbType.VarBinary, 64);
            update.Parameters.Add("@holot", System.Data.SqlDbType.VarBinary, 64);
            update.Parameters.Add("@ten", System.Data.SqlDbType.VarBinary, 64);
            update.Parameters.Add("@cmnd", System.Data.SqlDbType.VarBinary, 64);
            update.Parameters.Add("@sotaikhoan", System.Data.SqlDbType.VarBinary, 64);
            update.Parameters.Add("@mobile", System.Data.SqlDbType.VarBinary, 64);
            update.Parameters.Add("@id", System.Data.SqlDbType.Int);
            update.Prepare();

            foreach (EmployeeRow row in rows)
            {
                update.Parameters["@maNV"].Value = ToBlindIndex(row.MaNV) ?? (object)DBNull.Value;
                update.Parameters["@holot"].Value = ToBlindIndex(row.Holot) ?? (object)DBNull.Value;
                update.Parameters["@ten"].Value = ToBlindIndex(row.Ten) ?? (object)DBNull.Value;
                update.Parameters["@cmnd"].Value = ToBlindIndex(row.CMND) ?? (object)DBNull.Value;
                update.Parameters["@sotaikhoan"].Value = ToBlindIndex(row.Sotaikhoan) ?? (object)DBNull.Value;

                string mobileDigits = Regex.Replace(row.Mobile ?? string.Empty, @"\D", string.Empty);
                update.Parameters["@mobile"].Value = ToBlindIndex(mobileDigits) ?? (object)DBNull.Value;
                update.Parameters["@id"].Value = row.Id;
                update.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        totalUpdated += rows.Count;
        lastId = rows[rows.Count - 1].Id;
        Console.WriteLine("Da xu ly: " + totalUpdated + " nhan vien...");
    }

    Console.WriteLine("=== Hoan tat: " + totalUpdated + " nhan vien da duoc ghi de blind index. ===");
}

Dictionary<string, string> LoadDotEnv(string filePath)
{
    if (!File.Exists(filePath)) throw new FileNotFoundException("Khong tim thay .env.", filePath);
    Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (string rawLine in File.ReadLines(filePath))
    {
        string line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith("#")) continue;
        int separator = line.IndexOf('=');
        if (separator <= 0) continue;
        values[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim().Trim('"');
    }
    return values;
}

string GetSetting(string key)
{
    string value = Environment.GetEnvironmentVariable(key);
    if (string.IsNullOrWhiteSpace(value)) envFile.TryGetValue(key, out value);
    if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Thieu " + key + " trong .env.");
    return value;
}

int GetBatchSize()
{
    for (int index = 0; index < Args.Count - 1; index++)
    {
        int requested;
        if (Args[index].Equals("--batch-size", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(Args[index + 1], out requested))
            return requested < 1 ? 1 : (requested > 5000 ? 5000 : requested);
    }
    return DefaultBatchSize;
}

byte[] ToBlindIndex(string value)
{
    string normalized = NormalizeSearchIndexValue(value);
    if (string.IsNullOrWhiteSpace(normalized)) return null;
    byte[] digest;
    using (HMACSHA256 hmac = new HMACSHA256(hmacKey))
        digest = hmac.ComputeHash(Encoding.UTF8.GetBytes(normalized));
    return Encoding.UTF8.GetBytes("HMACSHA256:v1:" + Convert.ToBase64String(digest));
}

string NormalizeSearchIndexValue(string value)
{
    if (string.IsNullOrWhiteSpace(value)) return null;
    string normalized = value.Normalize(NormalizationForm.FormC).Trim();
    normalized = Regex.Replace(normalized, @"\s+", " ");
    return normalized.ToUpperInvariant();
}

List<EmployeeRow> ReadBatch(SqlConnection connection, int lastId, int size)
{
    List<EmployeeRow> rows = new List<EmployeeRow>();
    using (SqlCommand command = new SqlCommand(@"
        SELECT TOP (@size) Id_NV, MaNV, Holot, Ten, CMND, Sotaikhoan, Mobile
        FROM dbo.Tbl_Nhanvien WHERE Id_NV > @lastId ORDER BY Id_NV;", connection))
    {
        command.Parameters.AddWithValue("@size", size);
        command.Parameters.AddWithValue("@lastId", lastId);
        using (SqlDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                rows.Add(new EmployeeRow(Convert.ToInt32(reader.GetValue(0)),
                    reader.IsDBNull(1) ? null : reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6)));
            }
        }
    }
    return rows;
}

void EnsureMobileIndexSchema(SqlConnection connection)
{
    using (SqlCommand command = new SqlCommand(@"
        IF COL_LENGTH('dbo.Tbl_Nhanvien', 'I_Mobile') IS NULL
            ALTER TABLE dbo.Tbl_Nhanvien ADD I_Mobile VARBINARY(64) NULL;
        ELSE IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Tbl_Nhanvien')
                         AND name = 'I_Mobile' AND max_length <> -1 AND max_length < 64)
        BEGIN
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Tbl_Nhanvien') AND name = 'IX_TblNhanvien_I_Mobile')
                DROP INDEX IX_TblNhanvien_I_Mobile ON dbo.Tbl_Nhanvien;
            ALTER TABLE dbo.Tbl_Nhanvien ALTER COLUMN I_Mobile VARBINARY(64) NULL;
        END;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Tbl_Nhanvien') AND name = 'IX_TblNhanvien_I_Mobile')
            CREATE NONCLUSTERED INDEX IX_TblNhanvien_I_Mobile ON dbo.Tbl_Nhanvien(I_Mobile) WHERE I_Mobile IS NOT NULL;", connection))
        command.ExecuteNonQuery();
}

public class EmployeeRow
{
    public int Id; public string MaNV; public string Holot; public string Ten; public string CMND; public string Sotaikhoan; public string Mobile;
    public EmployeeRow(int id, string maNV, string holot, string ten, string cmnd, string sotaikhoan, string mobile)
    { Id = id; MaNV = maNV; Holot = holot; Ten = ten; CMND = cmnd; Sotaikhoan = sotaikhoan; Mobile = mobile; }
}
