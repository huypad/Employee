/*
  Search indexes for encrypted employee lookups.
  They support exact-match search after the API converts the keyword through
  EncryptionService.HashSearchIndex.  Each statement is idempotent.
*/
IF COL_LENGTH('dbo.Tbl_Nhanvien', 'I_MaNV') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Tbl_Nhanvien') AND name = 'IX_TblNhanvien_I_MaNV')
    CREATE NONCLUSTERED INDEX IX_TblNhanvien_I_MaNV ON dbo.Tbl_Nhanvien(I_MaNV) WHERE I_MaNV IS NOT NULL;
GO

IF COL_LENGTH('dbo.Tbl_Nhanvien', 'I_Holot') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Tbl_Nhanvien') AND name = 'IX_TblNhanvien_I_Holot')
    CREATE NONCLUSTERED INDEX IX_TblNhanvien_I_Holot ON dbo.Tbl_Nhanvien(I_Holot) WHERE I_Holot IS NOT NULL;
GO

IF COL_LENGTH('dbo.Tbl_Nhanvien', 'I_Ten') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Tbl_Nhanvien') AND name = 'IX_TblNhanvien_I_Ten')
    CREATE NONCLUSTERED INDEX IX_TblNhanvien_I_Ten ON dbo.Tbl_Nhanvien(I_Ten) WHERE I_Ten IS NOT NULL;
GO

IF COL_LENGTH('dbo.Tbl_Nhanvien', 'I_CMND') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Tbl_Nhanvien') AND name = 'IX_TblNhanvien_I_CMND')
    CREATE NONCLUSTERED INDEX IX_TblNhanvien_I_CMND ON dbo.Tbl_Nhanvien(I_CMND) WHERE I_CMND IS NOT NULL;
GO

IF COL_LENGTH('dbo.Tbl_Nhanvien', 'I_Sotaikhoan') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Tbl_Nhanvien') AND name = 'IX_TblNhanvien_I_Sotaikhoan')
    CREATE NONCLUSTERED INDEX IX_TblNhanvien_I_Sotaikhoan ON dbo.Tbl_Nhanvien(I_Sotaikhoan) WHERE I_Sotaikhoan IS NOT NULL;
GO

IF COL_LENGTH('dbo.Tbl_Nhanvien', 'CMNDHash') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Tbl_Nhanvien') AND name = 'IX_TblNhanvien_CMNDHash')
    CREATE NONCLUSTERED INDEX IX_TblNhanvien_CMNDHash ON dbo.Tbl_Nhanvien(CMNDHash) WHERE CMNDHash IS NOT NULL;
GO
