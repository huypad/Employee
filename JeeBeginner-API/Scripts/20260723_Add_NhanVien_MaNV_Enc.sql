/* Run once against databases which already have the older employee encryption columns. */
IF COL_LENGTH('dbo.Tbl_Nhanvien', 'MaNV_Enc') IS NULL
    ALTER TABLE dbo.Tbl_Nhanvien ADD MaNV_Enc NVARCHAR(MAX) NULL;
