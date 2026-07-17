/* Run once on databases that already have the previous encryption columns. */
IF COL_LENGTH('dbo.Tbl_Nhanvien', 'CMND_FPE') IS NULL
    ALTER TABLE dbo.Tbl_Nhanvien ADD CMND_FPE NVARCHAR(50) NULL;
