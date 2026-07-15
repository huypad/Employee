/*
 Run once only if Tbl_Nhanvien already has encrypted columns typed as VARBINARY.
 The current AES/HMAC service returns text values such as AESGCM:v1:... .
*/
ALTER TABLE dbo.Tbl_Nhanvien ALTER COLUMN Holot_Enc NVARCHAR(MAX) NULL;
ALTER TABLE dbo.Tbl_Nhanvien ALTER COLUMN Ten_Enc NVARCHAR(MAX) NULL;
ALTER TABLE dbo.Tbl_Nhanvien ALTER COLUMN CMND_Enc NVARCHAR(MAX) NULL;
ALTER TABLE dbo.Tbl_Nhanvien ALTER COLUMN CMNDHash NVARCHAR(128) NULL;
