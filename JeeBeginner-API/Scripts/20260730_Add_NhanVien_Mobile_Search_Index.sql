/*
  Blind index for employee phone lookup.

  IMPORTANT: the API stores UTF-8 bytes of the text returned by
  EncryptionService.HashSearchIndex(), e.g. "HMACSHA256:v1:<base64>".
  This is about 58 bytes, so VARBINARY(32) is not large enough.
  Values are deliberately backfilled by the API, not HASHBYTES in SQL,
  because the API owns the HMAC secret and its normalization rule.
*/
USE JeeBeginner;
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.Tbl_Nhanvien', 'I_Mobile') IS NULL
BEGIN
    ALTER TABLE dbo.Tbl_Nhanvien ADD I_Mobile VARBINARY(64) NULL;
END
ELSE IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Tbl_Nhanvien')
      AND name = 'I_Mobile'
      AND max_length < 64
)
BEGIN
    IF EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID('dbo.Tbl_Nhanvien')
          AND name = 'IX_TblNhanvien_I_Mobile'
    )
        DROP INDEX IX_TblNhanvien_I_Mobile ON dbo.Tbl_Nhanvien;

    ALTER TABLE dbo.Tbl_Nhanvien ALTER COLUMN I_Mobile VARBINARY(64) NULL;
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.Tbl_Nhanvien')
      AND name = 'IX_TblNhanvien_I_Mobile'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_TblNhanvien_I_Mobile
    ON dbo.Tbl_Nhanvien(I_Mobile)
    WHERE I_Mobile IS NOT NULL;
END

COMMIT TRANSACTION;
GO

-- Verify schema only.  Populate I_Mobile by calling RebuildSearchIndexes in the API.
SELECT
    c.name AS TenCot,
    t.name AS KieuDuLieu,
    c.max_length AS SoByte,
    i.name AS TenIndex,
    i.type_desc AS LoaiIndex
FROM sys.columns c
JOIN sys.types t ON t.user_type_id = c.user_type_id
LEFT JOIN sys.index_columns ic
    ON ic.object_id = c.object_id AND ic.column_id = c.column_id
LEFT JOIN sys.indexes i
    ON i.object_id = ic.object_id AND i.index_id = ic.index_id
WHERE c.object_id = OBJECT_ID('dbo.Tbl_Nhanvien')
  AND c.name = 'I_Mobile';
