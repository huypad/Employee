/*
  Import/generator compatibility: remove only employee input-format checks.
  Primary keys, foreign keys, UNIQUE constraints and NOT NULL constraints are untouched.
*/
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.Tbl_Nhanvien') AND name = 'CK_TblNhanvien_Mobile_Format')
    ALTER TABLE dbo.Tbl_Nhanvien DROP CONSTRAINT CK_TblNhanvien_Mobile_Format;
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.Tbl_Nhanvien') AND name = 'CK_TblNhanvien_CMND_Digits')
    ALTER TABLE dbo.Tbl_Nhanvien DROP CONSTRAINT CK_TblNhanvien_CMND_Digits;
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.Tbl_Nhanvien') AND name = 'CK_TblNhanvien_MaNV_Format')
    ALTER TABLE dbo.Tbl_Nhanvien DROP CONSTRAINT CK_TblNhanvien_MaNV_Format;
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.Tbl_Nhanvien') AND name = 'CK_TblNhanvien_Email_Format')
    ALTER TABLE dbo.Tbl_Nhanvien DROP CONSTRAINT CK_TblNhanvien_Email_Format;
GO
