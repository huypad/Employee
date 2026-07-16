/* Run this once against database JeeBeginner before creating or editing employees. */
IF COL_LENGTH('dbo.Tbl_Nhanvien', 'Holot_Enc') IS NULL
    ALTER TABLE dbo.Tbl_Nhanvien ADD Holot_Enc NVARCHAR(MAX) NULL;

IF COL_LENGTH('dbo.Tbl_Nhanvien', 'Ten_Enc') IS NULL
    ALTER TABLE dbo.Tbl_Nhanvien ADD Ten_Enc NVARCHAR(MAX) NULL;

IF COL_LENGTH('dbo.Tbl_Nhanvien', 'CMND_Enc') IS NULL
    ALTER TABLE dbo.Tbl_Nhanvien ADD CMND_Enc NVARCHAR(MAX) NULL;

IF COL_LENGTH('dbo.Tbl_Nhanvien', 'CMNDHash') IS NULL
    ALTER TABLE dbo.Tbl_Nhanvien ADD CMNDHash NVARCHAR(128) NULL;
