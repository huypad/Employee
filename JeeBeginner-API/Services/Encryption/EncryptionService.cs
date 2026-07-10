using JeeBeginner.Models.Encryption;
using Microsoft.Extensions.Configuration;
using Org.BouncyCastle.Crypto.Fpe;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Utilities;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace JeeBeginner.Services.Encryption
{
    public class EncryptionService : IEncryptionService
    {
        private const string AesPrefix = "AESGCM:v1";
        private const string RsaPrefix = "RSAHYBRID:v1";
        private const int AesNonceSize = 12;
        private const int AesTagSize = 16;
        private const string DigitAlphabet = "0123456789";
        private const string AlphaNumericAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        private static readonly Lazy<byte[]> LocalRsaPrivateKey = new Lazy<byte[]>(CreateLocalRsaPrivateKey);

        private readonly IConfiguration _configuration;
        private readonly byte[] _aesKey;
        private readonly byte[] _fpeKey;
        private readonly byte[] _fpeTweak;

        public EncryptionService(IConfiguration configuration)
        {
            _configuration = configuration;
            _aesKey = ResolveKey("Encryption:AesKey", 32);
            _fpeKey = ResolveKey("Encryption:FpeKey", 32);
            _fpeTweak = ResolveTweak();
        }

        public string EncryptAes(string plainText)
        {
            if (plainText == null) return null;

            byte[] nonce = new byte[AesNonceSize];
            byte[] tag = new byte[AesTagSize];
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] cipherBytes = new byte[plainBytes.Length];

            RandomNumberGenerator.Fill(nonce);
            using (AesGcm aes = new AesGcm(_aesKey, AesTagSize))
            {
                aes.Encrypt(nonce, plainBytes, cipherBytes, tag);
            }

            return string.Join(":",
                AesPrefix,
                Convert.ToBase64String(nonce),
                Convert.ToBase64String(tag),
                Convert.ToBase64String(cipherBytes));
        }

        public string DecryptAes(string cipherText)
        {
            if (cipherText == null) return null;
            string[] parts = cipherText.Split(':');
            if (parts.Length != 5 || $"{parts[0]}:{parts[1]}" != AesPrefix)
            {
                throw new ArgumentException("Chuoi AES khong dung dinh dang.");
            }

            byte[] nonce = Convert.FromBase64String(parts[2]);
            byte[] tag = Convert.FromBase64String(parts[3]);
            byte[] cipherBytes = Convert.FromBase64String(parts[4]);
            byte[] plainBytes = new byte[cipherBytes.Length];

            using (AesGcm aes = new AesGcm(_aesKey, AesTagSize))
            {
                aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
            }

            return Encoding.UTF8.GetString(plainBytes);
        }

        public string EncryptRsa(string plainText)
        {
            if (plainText == null) return null;

            byte[] dataKey = new byte[32];
            byte[] nonce = new byte[AesNonceSize];
            byte[] tag = new byte[AesTagSize];
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] cipherBytes = new byte[plainBytes.Length];

            RandomNumberGenerator.Fill(dataKey);
            RandomNumberGenerator.Fill(nonce);

            using (AesGcm aes = new AesGcm(dataKey, AesTagSize))
            {
                aes.Encrypt(nonce, plainBytes, cipherBytes, tag);
            }

            byte[] encryptedKey;
            using (RSA rsa = CreateRsaForEncryption())
            {
                encryptedKey = rsa.Encrypt(dataKey, RSAEncryptionPadding.OaepSHA256);
            }

            CryptographicOperations.ZeroMemory(dataKey);

            return string.Join(":",
                RsaPrefix,
                Convert.ToBase64String(encryptedKey),
                Convert.ToBase64String(nonce),
                Convert.ToBase64String(tag),
                Convert.ToBase64String(cipherBytes));
        }

        public string DecryptRsa(string cipherText)
        {
            if (cipherText == null) return null;
            string[] parts = cipherText.Split(':');
            if (parts.Length != 6 || $"{parts[0]}:{parts[1]}" != RsaPrefix)
            {
                throw new ArgumentException("Chuoi RSA hybrid khong dung dinh dang.");
            }

            byte[] encryptedKey = Convert.FromBase64String(parts[2]);
            byte[] nonce = Convert.FromBase64String(parts[3]);
            byte[] tag = Convert.FromBase64String(parts[4]);
            byte[] cipherBytes = Convert.FromBase64String(parts[5]);
            byte[] plainBytes = new byte[cipherBytes.Length];

            byte[] dataKey;
            using (RSA rsa = CreateRsaForDecryption())
            {
                dataKey = rsa.Decrypt(encryptedKey, RSAEncryptionPadding.OaepSHA256);
            }

            using (AesGcm aes = new AesGcm(dataKey, AesTagSize))
            {
                aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
            }

            CryptographicOperations.ZeroMemory(dataKey);
            return Encoding.UTF8.GetString(plainBytes);
        }

        public string EncryptFpeDigits(string plainText)
        {
            return ProcessFpe(plainText, DigitAlphabet, true);
        }

        public string DecryptFpeDigits(string cipherText)
        {
            return ProcessFpe(cipherText, DigitAlphabet, false);
        }

        public string EncryptFpeAlphaNumeric(string plainText)
        {
            return ProcessFpe(plainText, AlphaNumericAlphabet, true);
        }

        public string DecryptFpeAlphaNumeric(string cipherText)
        {
            return ProcessFpe(cipherText, AlphaNumericAlphabet, false);
        }

        public NhanVienCryptoModel EncryptNhanVienAes(NhanVienCryptoModel model)
        {
            if (model == null) return null;
            return new NhanVienCryptoModel
            {
                Id = model.Id,
                MaNV = model.MaNV,
                HoTen = EncryptAes(model.HoTen),
                SDT = EncryptAes(model.SDT),
                CCCD = EncryptAes(model.CCCD),
                Email = EncryptAes(model.Email),
                DiaChi = EncryptAes(model.DiaChi),
                PhongBan = model.PhongBan,
                ChucVu = model.ChucVu
            };
        }

        public NhanVienCryptoModel DecryptNhanVienAes(NhanVienCryptoModel model)
        {
            if (model == null) return null;
            return new NhanVienCryptoModel
            {
                Id = model.Id,
                MaNV = model.MaNV,
                HoTen = DecryptAes(model.HoTen),
                SDT = DecryptAes(model.SDT),
                CCCD = DecryptAes(model.CCCD),
                Email = DecryptAes(model.Email),
                DiaChi = DecryptAes(model.DiaChi),
                PhongBan = model.PhongBan,
                ChucVu = model.ChucVu
            };
        }

        public NhanVienCryptoModel EncryptNhanVienRsa(NhanVienCryptoModel model)
        {
            if (model == null) return null;
            return new NhanVienCryptoModel
            {
                Id = model.Id,
                MaNV = model.MaNV,
                HoTen = EncryptRsa(model.HoTen),
                SDT = EncryptRsa(model.SDT),
                CCCD = EncryptRsa(model.CCCD),
                Email = EncryptRsa(model.Email),
                DiaChi = EncryptRsa(model.DiaChi),
                PhongBan = model.PhongBan,
                ChucVu = model.ChucVu
            };
        }

        public NhanVienCryptoModel DecryptNhanVienRsa(NhanVienCryptoModel model)
        {
            if (model == null) return null;
            return new NhanVienCryptoModel
            {
                Id = model.Id,
                MaNV = model.MaNV,
                HoTen = DecryptRsa(model.HoTen),
                SDT = DecryptRsa(model.SDT),
                CCCD = DecryptRsa(model.CCCD),
                Email = DecryptRsa(model.Email),
                DiaChi = DecryptRsa(model.DiaChi),
                PhongBan = model.PhongBan,
                ChucVu = model.ChucVu
            };
        }

        public NhanVienCryptoModel EncryptNhanVienFpe(NhanVienCryptoModel model)
        {
            if (model == null) return null;
            return new NhanVienCryptoModel
            {
                Id = model.Id,
                MaNV = EncryptFpeAlphaNumeric(model.MaNV),
                HoTen = model.HoTen,
                SDT = EncryptFpeDigits(model.SDT),
                CCCD = EncryptFpeDigits(model.CCCD),
                Email = model.Email,
                DiaChi = model.DiaChi,
                PhongBan = model.PhongBan,
                ChucVu = model.ChucVu
            };
        }

        public NhanVienCryptoModel DecryptNhanVienFpe(NhanVienCryptoModel model)
        {
            if (model == null) return null;
            return new NhanVienCryptoModel
            {
                Id = model.Id,
                MaNV = DecryptFpeAlphaNumeric(model.MaNV),
                HoTen = model.HoTen,
                SDT = DecryptFpeDigits(model.SDT),
                CCCD = DecryptFpeDigits(model.CCCD),
                Email = model.Email,
                DiaChi = model.DiaChi,
                PhongBan = model.PhongBan,
                ChucVu = model.ChucVu
            };
        }

        private string ProcessFpe(string value, string alphabet, bool encrypt)
        {
            if (value == null) return null;
            if (value.Length == 0) return value;

            char[] alphabetChars = alphabet.ToCharArray();
            char[] selectedChars = value.Where(c => alphabet.IndexOf(c) >= 0).ToArray();
            if (selectedChars.Length == 0) return value;

            EnsureFpeDomainSize(alphabet.Length, selectedChars.Length);

            BasicAlphabetMapper mapper = new BasicAlphabetMapper(alphabetChars);
            byte[] input = mapper.ConvertToIndexes(selectedChars);
            byte[] output = new byte[input.Length];

            FpeFf1Engine engine = new FpeFf1Engine();
            engine.Init(encrypt, new FpeParameters(new KeyParameter(_fpeKey), mapper.Radix, _fpeTweak));
            engine.ProcessBlock(input, 0, input.Length, output, 0);

            char[] mappedChars = mapper.ConvertToChars(output);
            int mappedIndex = 0;
            char[] result = value.ToCharArray();
            for (int i = 0; i < result.Length; i++)
            {
                if (alphabet.IndexOf(result[i]) >= 0)
                {
                    result[i] = mappedChars[mappedIndex++];
                }
            }

            return new string(result);
        }

        private void EnsureFpeDomainSize(int radix, int length)
        {
            decimal domainSize = 1;
            for (int i = 0; i < length; i++)
            {
                domainSize *= radix;
                if (domainSize >= 1000000) return;
            }

            throw new ArgumentException("FPE FF1 can mien du lieu toi thieu 1,000,000. Hay truyen gia tri dai hon.");
        }

        private byte[] ResolveKey(string configKey, int fallbackSize)
        {
            string configured = _configuration.GetValue<string>(configKey);
            if (!string.IsNullOrWhiteSpace(configured))
            {
                try
                {
                    byte[] key = Convert.FromBase64String(configured);
                    if (key.Length == 16 || key.Length == 24 || key.Length == 32)
                    {
                        return key;
                    }
                }
                catch (FormatException)
                {
                }
            }

            string seed = configured;
            if (string.IsNullOrWhiteSpace(seed))
            {
                seed = _configuration.GetValue<string>("JWT:Secret");
            }
            if (string.IsNullOrWhiteSpace(seed))
            {
                seed = "JeeBeginner.Local.Encryption.Key";
            }

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
            if (fallbackSize == hash.Length) return hash;

            byte[] keyBytes = new byte[fallbackSize];
            Array.Copy(hash, keyBytes, keyBytes.Length);
            return keyBytes;
        }

        private byte[] ResolveTweak()
        {
            string configured = _configuration.GetValue<string>("Encryption:FpeTweak");
            if (string.IsNullOrWhiteSpace(configured))
            {
                configured = "JeeBeginnerFpeTweak";
            }

            try
            {
                return Convert.FromBase64String(configured);
            }
            catch (FormatException)
            {
                return Encoding.UTF8.GetBytes(configured);
            }
        }

        private RSA CreateRsaForEncryption()
        {
            string publicKey = _configuration.GetValue<string>("Encryption:RsaPublicKey");
            if (!string.IsNullOrWhiteSpace(publicKey))
            {
                RSA rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
                return rsa;
            }

            RSA fallback = CreateRsaForDecryption();
            byte[] publicKeyBytes = fallback.ExportSubjectPublicKeyInfo();
            fallback.Dispose();

            RSA publicRsa = RSA.Create();
            publicRsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
            return publicRsa;
        }

        private RSA CreateRsaForDecryption()
        {
            string privateKey = _configuration.GetValue<string>("Encryption:RsaPrivateKey");
            byte[] privateKeyBytes = !string.IsNullOrWhiteSpace(privateKey)
                ? Convert.FromBase64String(privateKey)
                : LocalRsaPrivateKey.Value;

            RSA rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
            return rsa;
        }

        private static byte[] CreateLocalRsaPrivateKey()
        {
            using (RSA rsa = RSA.Create(2048))
            {
                return rsa.ExportPkcs8PrivateKey();
            }
        }
    }
}
