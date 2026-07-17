/*
  Normalize legacy employee data before enabling format constraints.
  - CCCD/CMND having 10 or 11 digits is left-padded to 12 digits.
  - 9- and 12-digit values are kept unchanged.
  - Phone numbers having 9 digits are prefixed with 0.
  - Rows which cannot be safely normalized are reported for manual correction.
  Run this script, then call POST /api/nhanvienmanagement/EncryptExistingNhanViens
  so CMND_Enc (RSA), CMND_FPE and CMNDHash are regenerated.
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

/* Normalize code display without changing its numeric suffix. */
UPDATE dbo.Tbl_Nhanvien
SET MaNV = UPPER(LTRIM(RTRIM(MaNV)))
WHERE MaNV IS NOT NULL AND MaNV <> UPPER(LTRIM(RTRIM(MaNV)));

/* Stop rather than create duplicate CCCDs after padding. */
IF EXISTS (
    SELECT NormalizedCMND
    FROM (
        SELECT CASE
            WHEN CMND NOT LIKE '%[^0-9]%' AND LEN(LTRIM(RTRIM(CMND))) BETWEEN 10 AND 11
                THEN RIGHT(REPLICATE('0', 12) + LTRIM(RTRIM(CMND)), 12)
            ELSE LTRIM(RTRIM(CMND))
        END AS NormalizedCMND
        FROM dbo.Tbl_Nhanvien
        WHERE NULLIF(LTRIM(RTRIM(CMND)), '') IS NOT NULL
    ) AS Normalized
    GROUP BY NormalizedCMND
    HAVING COUNT(*) > 1
)
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50001, 'Du lieu CCCD bi trung sau khi chuan hoa. Khong co thay doi nao duoc luu.', 1;
END;

/* Pad only numeric 10- or 11-digit legacy values. Clear derived encrypted values. */
UPDATE dbo.Tbl_Nhanvien
SET CMND = RIGHT(REPLICATE('0', 12) + LTRIM(RTRIM(CMND)), 12),
    CMND_Enc = NULL,
    CMND_FPE = NULL,
    CMNDHash = NULL,
    LastModified = GETDATE()
WHERE CMND NOT LIKE '%[^0-9]%'
  AND LEN(LTRIM(RTRIM(CMND))) BETWEEN 10 AND 11;

/* A 9-digit phone is safely converted to a Vietnamese 10-digit phone. */
UPDATE dbo.Tbl_Nhanvien
SET Mobile = '0' + LTRIM(RTRIM(Mobile)),
    LastModified = GETDATE()
WHERE Mobile NOT LIKE '%[^0-9]%'
  AND LEN(LTRIM(RTRIM(Mobile))) = 9;

/* Remove accidental outer spaces from optional email/address fields. */
UPDATE dbo.Tbl_Nhanvien
SET Email = NULLIF(LTRIM(RTRIM(Email)), ''),
    Thuongtru_diachi = NULLIF(LTRIM(RTRIM(Thuongtru_diachi)), '')
WHERE (Email IS NOT NULL AND Email <> LTRIM(RTRIM(Email)))
   OR (Thuongtru_diachi IS NOT NULL AND Thuongtru_diachi <> LTRIM(RTRIM(Thuongtru_diachi)));

COMMIT TRANSACTION;
GO

/* Review any remaining invalid values. Correct these rows manually before enforcing constraints. */
SELECT
    Id_NV, MaNV, Mobile, CMND, Email,
    CASE
        WHEN MaNV IS NULL OR MaNV NOT LIKE 'NV%' OR SUBSTRING(MaNV, 3, LEN(MaNV)) LIKE '%[^0-9]%' THEN N'Mã NV không đúng dạng NV + số'
        WHEN CMND IS NULL OR CMND LIKE '%[^0-9]%' OR LEN(CMND) NOT IN (9, 12) THEN N'CCCD/CMND không đúng 9 hoặc 12 số'
        WHEN Mobile IS NOT NULL AND LTRIM(RTRIM(Mobile)) <> '' AND (Mobile LIKE '%[^0-9]%' OR LEN(Mobile) <> 10 OR Mobile NOT LIKE '0%') THEN N'SĐT không đúng 10 số bắt đầu 0'
        WHEN Email IS NOT NULL AND LTRIM(RTRIM(Email)) <> '' AND (Email LIKE '% %' OR Email NOT LIKE '%_@_%._%' OR LEN(Email) > 100) THEN N'Email không đúng định dạng'
    END AS LoiCanSua
FROM dbo.Tbl_Nhanvien
WHERE MaNV IS NULL OR MaNV NOT LIKE 'NV%' OR SUBSTRING(MaNV, 3, LEN(MaNV)) LIKE '%[^0-9]%'
   OR CMND IS NULL OR CMND LIKE '%[^0-9]%' OR LEN(CMND) NOT IN (9, 12)
   OR (Mobile IS NOT NULL AND LTRIM(RTRIM(Mobile)) <> '' AND (Mobile LIKE '%[^0-9]%' OR LEN(Mobile) <> 10 OR Mobile NOT LIKE '0%'))
   OR (Email IS NOT NULL AND LTRIM(RTRIM(Email)) <> '' AND (Email LIKE '% %' OR Email NOT LIKE '%_@_%._%' OR LEN(Email) > 100));
