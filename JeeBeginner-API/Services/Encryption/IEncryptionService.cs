using JeeBeginner.Models.Encryption;

namespace JeeBeginner.Services.Encryption
{
    public interface IEncryptionService
    {
        string EncryptAes(string plainText);
        string DecryptAes(string cipherText);

        string EncryptRsa(string plainText);
        string DecryptRsa(string cipherText);

        string HashHmacSha256(string plainText);
        string HashSearchIndex(string plainText);
        string EncryptFpeDigits(string plainText);
        string DecryptFpeDigits(string cipherText);

        string EncryptFpeAlphaNumeric(string plainText);
        string DecryptFpeAlphaNumeric(string cipherText);

        NhanVienCryptoModel EncryptNhanVienAes(NhanVienCryptoModel model);
        NhanVienCryptoModel DecryptNhanVienAes(NhanVienCryptoModel model);

        // Ho ten dung AES; CCCD luu ca RSA va FPE.
        NhanVienCryptoModel EncryptNhanVienWithRsaAndFpeCccd(NhanVienCryptoModel model);

        NhanVienCryptoModel EncryptNhanVienRsa(NhanVienCryptoModel model);
        NhanVienCryptoModel DecryptNhanVienRsa(NhanVienCryptoModel model);

        NhanVienCryptoModel EncryptNhanVienFpe(NhanVienCryptoModel model);
        NhanVienCryptoModel DecryptNhanVienFpe(NhanVienCryptoModel model);

        NhanVienCryptoModel HashNhanVienHmacSha256(NhanVienCryptoModel model);
    }
}
