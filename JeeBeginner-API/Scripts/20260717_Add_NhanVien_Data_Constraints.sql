/* Run once after reviewing duplicate MaNV rows. Existing legacy rows are retained;
   the CHECK constraints protect new inserts and updates. */

IF EXISTS (
    SELECT MaNV
    FROM dbo.Tbl_Nhanvien
    WHERE NULLIF(LTRIM(RTRIM(MaNV)), '') IS NOT NULL
    GROUP BY MaNV
    HAVING COUNT(*) > 1
)
    PRINT N'Khong tao unique index MaNV vi du lieu hien tai co MaNV trung. Hay xu ly cac dong trung truoc.';
ELSE IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Tbl_Nhanvien_MaNV' AND object_id = OBJECT_ID('dbo.Tbl_Nhanvien'))
    CREATE UNIQUE INDEX UX_Tbl_Nhanvien_MaNV ON dbo.Tbl_Nhanvien(MaNV)
    WHERE MaNV IS NOT NULL AND MaNV <> '';
GO

IF EXISTS (
    SELECT CMND
    FROM dbo.Tbl_Nhanvien
    WHERE NULLIF(LTRIM(RTRIM(CMND)), '') IS NOT NULL
    GROUP BY CMND
    HAVING COUNT(*) > 1
)
    PRINT N'Khong tao unique index CCCD vi du lieu hien tai co CCCD trung. Hay xu ly cac dong trung truoc.';
ELSE IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Tbl_Nhanvien_CMND' AND object_id = OBJECT_ID('dbo.Tbl_Nhanvien'))
    CREATE UNIQUE INDEX UX_Tbl_Nhanvien_CMND ON dbo.Tbl_Nhanvien(CMND)
    WHERE CMND IS NOT NULL AND CMND <> '';
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_TblNhanvien_CMND_Digits')
    ALTER TABLE dbo.Tbl_Nhanvien WITH NOCHECK ADD CONSTRAINT CK_TblNhanvien_CMND_Digits
    CHECK (CMND IS NULL OR LTRIM(RTRIM(CMND)) = '' OR (CMND NOT LIKE '%[^0-9]%' AND LEN(CMND) IN (9, 12)));
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_TblNhanvien_Mobile_Format')
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

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_TblNhanvien_Status')
    ALTER TABLE dbo.Tbl_Nhanvien WITH NOCHECK ADD CONSTRAINT CK_TblNhanvien_Status
    CHECK (Status IS NULL OR Status IN (0, 1));
GO
