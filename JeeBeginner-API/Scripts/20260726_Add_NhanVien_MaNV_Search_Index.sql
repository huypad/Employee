/*
  Add the deterministic hash-index columns used by encrypted employee search.
  The statements are idempotent: existing columns are kept unchanged.
*/
IF COL_LENGTH('dbo.Tbl_Nhanvien', 'I_MaNV') IS NULL
    ALTER TABLE dbo.Tbl_Nhanvien ADD I_MaNV VARBINARY(128) NULL;

IF COL_LENGTH('dbo.Tbl_Nhanvien', 'DiaChi') IS NULL
    ALTER TABLE dbo.Tbl_Nhanvien ADD DiaChi NVARCHAR(255) NULL;

IF COL_LENGTH('dbo.Tbl_Nhanvien', 'I_Holot') IS NULL
    ALTER TABLE dbo.Tbl_Nhanvien ADD I_Holot VARBINARY(128) NULL;

IF COL_LENGTH('dbo.Tbl_Nhanvien', 'I_Ten') IS NULL
    ALTER TABLE dbo.Tbl_Nhanvien ADD I_Ten VARBINARY(128) NULL;

IF COL_LENGTH('dbo.Tbl_Nhanvien', 'I_CMND') IS NULL
    ALTER TABLE dbo.Tbl_Nhanvien ADD I_CMND VARBINARY(128) NULL;

IF COL_LENGTH('dbo.Tbl_Nhanvien', 'I_Sotaikhoan') IS NULL
    ALTER TABLE dbo.Tbl_Nhanvien ADD I_Sotaikhoan VARBINARY(128) NULL;

/*
  Older databases stored search hashes as VARBINARY(MAX). SQL Server cannot
  create a key index on a MAX column. The API's versioned HMAC search hash is
  shorter than 128 bytes. Abort instead of truncating if an unexpected value
  is found.
*/
IF COL_LENGTH('dbo.Tbl_Nhanvien', 'I_Holot') IS NOT NULL
   AND EXISTS (SELECT 1 FROM dbo.Tbl_Nhanvien WHERE DATALENGTH(I_Holot) > 128)
    THROW 50001, 'I_Holot has a value longer than 128 bytes; no schema change was made.', 1;

IF COL_LENGTH('dbo.Tbl_Nhanvien', 'I_Ten') IS NOT NULL
   AND EXISTS (SELECT 1 FROM dbo.Tbl_Nhanvien WHERE DATALENGTH(I_Ten) > 128)
    THROW 50002, 'I_Ten has a value longer than 128 bytes; no schema change was made.', 1;

IF COL_LENGTH('dbo.Tbl_Nhanvien', 'I_CMND') IS NOT NULL
   AND EXISTS (SELECT 1 FROM dbo.Tbl_Nhanvien WHERE DATALENGTH(I_CMND) > 128)
    THROW 50003, 'I_CMND has a value longer than 128 bytes; no schema change was made.', 1;

IF COL_LENGTH('dbo.Tbl_Nhanvien', 'I_Sotaikhoan') IS NOT NULL
   AND EXISTS (SELECT 1 FROM dbo.Tbl_Nhanvien WHERE DATALENGTH(I_Sotaikhoan) > 128)
    THROW 50004, 'I_Sotaikhoan has a value longer than 128 bytes; no schema change was made.', 1;

/* Drop only the rebuildable encrypted-search indexes before changing key types. */
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Tbl_Nhanvien') AND name = 'IX_TblNhanvien_I_Holot')
    DROP INDEX IX_TblNhanvien_I_Holot ON dbo.Tbl_Nhanvien;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Tbl_Nhanvien') AND name = 'IX_TblNhanvien_I_Ten')
    DROP INDEX IX_TblNhanvien_I_Ten ON dbo.Tbl_Nhanvien;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Tbl_Nhanvien') AND name = 'IX_TblNhanvien_I_CMND')
    DROP INDEX IX_TblNhanvien_I_CMND ON dbo.Tbl_Nhanvien;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Tbl_Nhanvien') AND name = 'IX_TblNhanvien_I_Sotaikhoan')
    DROP INDEX IX_TblNhanvien_I_Sotaikhoan ON dbo.Tbl_Nhanvien;

IF COL_LENGTH('dbo.Tbl_Nhanvien', 'I_Holot') IS NOT NULL
    ALTER TABLE dbo.Tbl_Nhanvien ALTER COLUMN I_Holot VARBINARY(128) NULL;

IF COL_LENGTH('dbo.Tbl_Nhanvien', 'I_Ten') IS NOT NULL
    ALTER TABLE dbo.Tbl_Nhanvien ALTER COLUMN I_Ten VARBINARY(128) NULL;

IF COL_LENGTH('dbo.Tbl_Nhanvien', 'I_CMND') IS NOT NULL
    ALTER TABLE dbo.Tbl_Nhanvien ALTER COLUMN I_CMND VARBINARY(128) NULL;

IF COL_LENGTH('dbo.Tbl_Nhanvien', 'I_Sotaikhoan') IS NOT NULL
    ALTER TABLE dbo.Tbl_Nhanvien ALTER COLUMN I_Sotaikhoan VARBINARY(128) NULL;
