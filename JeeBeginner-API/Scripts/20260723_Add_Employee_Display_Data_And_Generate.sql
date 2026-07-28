/*
  Adds the employee-management display columns when working with the legacy
  Tbl_Nhanvien schema, then fills only missing values with deterministic data.
  Existing email, department, job title and address values are preserved.
*/
IF COL_LENGTH('dbo.Tbl_Nhanvien', 'DiaChi') IS NULL
    ALTER TABLE dbo.Tbl_Nhanvien ADD DiaChi NVARCHAR(255) NULL;

IF COL_LENGTH('dbo.Tbl_Nhanvien', 'PhongBan') IS NULL
    ALTER TABLE dbo.Tbl_Nhanvien ADD PhongBan NVARCHAR(100) NULL;

IF COL_LENGTH('dbo.Tbl_Nhanvien', 'Tenchucvu') IS NULL
    ALTER TABLE dbo.Tbl_Nhanvien ADD Tenchucvu NVARCHAR(100) NULL;
GO

;WITH MissingData AS
(
    SELECT Id_NV,
           ABS(CHECKSUM(CONVERT(NVARCHAR(50), Id_NV))) % 6 AS SampleIndex
    FROM dbo.Tbl_Nhanvien
)
UPDATE nv
SET Email = CASE WHEN NULLIF(LTRIM(RTRIM(nv.Email)), '') IS NULL
                 THEN CONCAT(N'nhanvien', nv.Id_NV, N'@jeework.local')
                 ELSE nv.Email END,
    PhongBan = CASE WHEN NULLIF(LTRIM(RTRIM(nv.PhongBan)), '') IS NULL THEN
                    CASE d.SampleIndex
                        WHEN 0 THEN N'Phòng Nhân sự'
                        WHEN 1 THEN N'Phòng Kế toán'
                        WHEN 2 THEN N'Phòng Kinh doanh'
                        WHEN 3 THEN N'Phòng Kỹ thuật'
                        WHEN 4 THEN N'Phòng Hành chính'
                        ELSE N'Phòng Công nghệ thông tin'
                    END
                    ELSE nv.PhongBan END,
    Tenchucvu = CASE WHEN NULLIF(LTRIM(RTRIM(nv.Tenchucvu)), '') IS NULL THEN
                    CASE d.SampleIndex
                        WHEN 0 THEN N'Nhân viên'
                        WHEN 1 THEN N'Chuyên viên'
                        WHEN 2 THEN N'Trưởng nhóm'
                        WHEN 3 THEN N'Kỹ sư'
                        WHEN 4 THEN N'Kế toán viên'
                        ELSE N'Quản lý'
                    END
                    ELSE nv.Tenchucvu END,
    LastModified = GETDATE()
FROM dbo.Tbl_Nhanvien AS nv
INNER JOIN MissingData AS d ON d.Id_NV = nv.Id_NV
WHERE NULLIF(LTRIM(RTRIM(nv.Email)), '') IS NULL
   OR NULLIF(LTRIM(RTRIM(nv.PhongBan)), '') IS NULL
   OR NULLIF(LTRIM(RTRIM(nv.Tenchucvu)), '') IS NULL;
GO
