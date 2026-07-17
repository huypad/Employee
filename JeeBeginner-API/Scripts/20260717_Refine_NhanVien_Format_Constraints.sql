/* Run once if 20260717_Add_NhanVien_Data_Constraints.sql was already run. */

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_TblNhanvien_CMND_Digits')
    ALTER TABLE dbo.Tbl_Nhanvien DROP CONSTRAINT CK_TblNhanvien_CMND_Digits;
GO
ALTER TABLE dbo.Tbl_Nhanvien WITH NOCHECK ADD CONSTRAINT CK_TblNhanvien_CMND_Digits
CHECK (CMND IS NULL OR LTRIM(RTRIM(CMND)) = '' OR (CMND NOT LIKE '%[^0-9]%' AND LEN(CMND) IN (9, 12)));
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_TblNhanvien_Mobile_Format')
    ALTER TABLE dbo.Tbl_Nhanvien DROP CONSTRAINT CK_TblNhanvien_Mobile_Format;
GO
ALTER TABLE dbo.Tbl_Nhanvien WITH NOCHECK ADD CONSTRAINT CK_TblNhanvien_Mobile_Format
CHECK (Mobile IS NULL OR LTRIM(RTRIM(Mobile)) = '' OR (Mobile NOT LIKE '%[^0-9]%' AND LEN(Mobile) = 10 AND Mobile LIKE '0%'));
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_TblNhanvien_MaNV_Format')
    ALTER TABLE dbo.Tbl_Nhanvien WITH NOCHECK ADD CONSTRAINT CK_TblNhanvien_MaNV_Format
    CHECK (MaNV IS NULL OR LTRIM(RTRIM(MaNV)) = '' OR (LEN(MaNV) BETWEEN 3 AND 12 AND MaNV LIKE 'NV%' AND SUBSTRING(MaNV, 3, LEN(MaNV)) NOT LIKE '%[^0-9]%'));
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_TblNhanvien_Email_Format')
    ALTER TABLE dbo.Tbl_Nhanvien WITH NOCHECK ADD CONSTRAINT CK_TblNhanvien_Email_Format
    CHECK (Email IS NULL OR LTRIM(RTRIM(Email)) = '' OR (LEN(Email) <= 100 AND Email NOT LIKE '% %' AND Email LIKE '%_@_%._%'));
GO
