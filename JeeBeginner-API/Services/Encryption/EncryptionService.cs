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
        private const string HmacSha256Prefix = "HMACSHA256:v1";
        private const int AesNonceSize = 12;
        private const int AesTagSize = 16;
        private const string DigitAlphabet = "0123456789";
        private const string AlphaNumericAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        private readonly IConfiguration _configuration;
        private readonly byte[] _aesKey;
        private readonly byte[] _fpeKey;
        private readonly byte[] _fpeTweak;
        private readonly byte[] _hmacKey;

        public EncryptionService(IConfiguration configuration)
        {
            _configuration = configuration;
            _aesKey = ResolveRequiredBase64Key("Encryption:AesKey", 32);
            _fpeKey = ResolveRequiredBase64Key("Encryption:FpeKey", 32);
            _fpeTweak = ResolveTweak();
            _hmacKey = ResolveRequiredBase64Key("Encryption:HmacKey", 32);
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

        public string HashHmacSha256(string plainText)
        {
            if (plainText == null) return null;

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] hashBytes;
            using (HMACSHA256 hmac = new HMACSHA256(_hmacKey))
            {
                hashBytes = hmac.ComputeHash(plainBytes);
            }

            return $"{HmacSha256Prefix}:{Convert.ToBase64String(hashBytes)}";
        }


        public string EncryptFpeDigits(string plainText)
        {
            return ProcessFpe(plainText, DigitAlphabet, true);
        }

        public string DecryptFpeDigits(string cipherText)
        {
            return ProcessFpe(cipherText, DigitAlphabet, false);
        }

        // public string EncryptFpeAlphaNumeric(string plainText)
        // {
        //     return ProcessFpe(plainText, AlphaNumericAlphabet, true);
        // }

        // public string DecryptFpeAlphaNumeric(string cipherText)
        // {
        //     return ProcessFpe(cipherText, AlphaNumericAlphabet, false);
        // }
        private const int MinAlphaNumericLength = 4;
        private const char AlphaPadChar = 'Q';        

        public string EncryptFpeAlphaNumeric(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;
            int meaningfulLength = plainText.Count(c => AlphaNumericAlphabet.IndexOf(c) >= 0);

            if (meaningfulLength >= MinAlphaNumericLength)
                return ProcessFpe(plainText, AlphaNumericAlphabet, true);

            int originalLength = plainText.Length;
            string padded = plainText.PadRight(plainText.Length + (MinAlphaNumericLength - meaningfulLength), AlphaPadChar);
            string encrypted = ProcessFpe(padded, AlphaNumericAlphabet, true);
            return $"~{originalLength}~{encrypted}";
        }

        public string DecryptFpeAlphaNumeric(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            if (cipherText.StartsWith("~"))
            {
                int endMarker = cipherText.IndexOf('~', 1);
                int originalLength = int.Parse(cipherText.Substring(1, endMarker - 1));
                string encryptedPart = cipherText.Substring(endMarker + 1);

                string decryptedPadded = ProcessFpe(encryptedPart, AlphaNumericAlphabet, false);
                return decryptedPadded.Substring(0, originalLength);
            }

            return ProcessFpe(cipherText, AlphaNumericAlphabet, false);
        }

        public NhanVienCryptoModel EncryptNhanVienAes(NhanVienCryptoModel model)
        {
            if (model == null) return null;
            return new NhanVienCryptoModel
            {
                Id = model.Id,
                I_Holot = model.I_Holot,
                I_Ten = model.I_Ten,
                I_CMND = model.I_CMND,
                I_Sotaikhoan = model.I_Sotaikhoan,
                Holot_Enc = EncryptAes(model.I_Holot),
                Ten_Enc = EncryptAes(model.I_Ten),
                CMND_Enc = EncryptAes(model.I_CMND),
                Sotaikhoan_Enc = EncryptAes(model.I_Sotaikhoan),
                CMNDHash = HashHmacSha256(model.I_CMND),
                SotaikhoanHash = HashHmacSha256(model.I_Sotaikhoan)
            };
        }

        public NhanVienCryptoModel DecryptNhanVienAes(NhanVienCryptoModel model)
        {
            if (model == null) return null;
            return new NhanVienCryptoModel
            {
                Id = model.Id,
                I_Holot = DecryptAes(model.Holot_Enc),
                I_Ten = DecryptAes(model.Ten_Enc),
                I_CMND = DecryptAes(model.CMND_Enc),
                I_Sotaikhoan = DecryptAes(model.Sotaikhoan_Enc),
                Holot_Enc = model.Holot_Enc,
                Ten_Enc = model.Ten_Enc,
                CMND_Enc = model.CMND_Enc,
                Sotaikhoan_Enc = model.Sotaikhoan_Enc,
                CMNDHash = model.CMNDHash,
                SotaikhoanHash = model.SotaikhoanHash
            };
        }

        public NhanVienCryptoModel EncryptNhanVienWithRsaAndFpeCccd(NhanVienCryptoModel model)
        {
            if (model == null) return null;

            return new NhanVienCryptoModel
            {
                Id = model.Id,
                I_Holot = model.I_Holot,
                I_Ten = model.I_Ten,
                I_CMND = model.I_CMND,
                I_Sotaikhoan = model.I_Sotaikhoan,
                Holot_Enc = EncryptAes(model.I_Holot),
                Ten_Enc = EncryptAes(model.I_Ten),
                CMND_Enc = EncryptRsa(model.I_CMND),
                CMND_FPE = EncryptFpeDigits(model.I_CMND),
                Sotaikhoan_Enc = model.Sotaikhoan_Enc,
                CMNDHash = HashHmacSha256(model.I_CMND),
                SotaikhoanHash = model.SotaikhoanHash
            };
        }

        public NhanVienCryptoModel EncryptNhanVienRsa(NhanVienCryptoModel model)
        {
            if (model == null) return null;
            return new NhanVienCryptoModel
            {
                Id = model.Id,
                I_Holot = model.I_Holot,
                I_Ten = model.I_Ten,
                I_CMND = model.I_CMND,
                I_Sotaikhoan = model.I_Sotaikhoan,
                Holot_Enc = EncryptRsa(model.I_Holot),
                Ten_Enc = EncryptRsa(model.I_Ten),
                CMND_Enc = EncryptRsa(model.I_CMND),
                Sotaikhoan_Enc = EncryptRsa(model.I_Sotaikhoan),
                CMNDHash = HashHmacSha256(model.I_CMND),
                SotaikhoanHash = HashHmacSha256(model.I_Sotaikhoan)
            };
        }

        public NhanVienCryptoModel DecryptNhanVienRsa(NhanVienCryptoModel model)
        {
            if (model == null) return null;
            return new NhanVienCryptoModel
            {
                Id = model.Id,
                I_Holot = DecryptRsa(model.Holot_Enc),
                I_Ten = DecryptRsa(model.Ten_Enc),
                I_CMND = DecryptRsa(model.CMND_Enc),
                I_Sotaikhoan = DecryptRsa(model.Sotaikhoan_Enc),
                Holot_Enc = model.Holot_Enc,
                Ten_Enc = model.Ten_Enc,
                CMND_Enc = model.CMND_Enc,
                Sotaikhoan_Enc = model.Sotaikhoan_Enc,
                CMNDHash = model.CMNDHash,
                SotaikhoanHash = model.SotaikhoanHash
            };
        }

        public NhanVienCryptoModel EncryptNhanVienFpe(NhanVienCryptoModel model)
        {
            if (model == null) return null;
            return new NhanVienCryptoModel
            {
                Id = model.Id,
                I_Holot = model.I_Holot,
                I_Ten = model.I_Ten,
                I_CMND = model.I_CMND,
                I_Sotaikhoan = model.I_Sotaikhoan,
                Holot_Enc = model.Holot_Enc,
                Ten_Enc = model.Ten_Enc,
                CMND_Enc = EncryptFpeDigits(model.I_CMND),
                Sotaikhoan_Enc = EncryptFpeDigits(model.I_Sotaikhoan),
                CMNDHash = HashHmacSha256(model.I_CMND),
                SotaikhoanHash = HashHmacSha256(model.I_Sotaikhoan)
            };
        }

        public NhanVienCryptoModel DecryptNhanVienFpe(NhanVienCryptoModel model)
        {
            if (model == null) return null;
            return new NhanVienCryptoModel
            {
                Id = model.Id,
                I_Holot = model.I_Holot,
                I_Ten = model.I_Ten,
                I_CMND = DecryptFpeDigits(model.CMND_Enc),
                I_Sotaikhoan = DecryptFpeDigits(model.Sotaikhoan_Enc),
                Holot_Enc = model.Holot_Enc,
                Ten_Enc = model.Ten_Enc,
                CMND_Enc = model.CMND_Enc,
                Sotaikhoan_Enc = model.Sotaikhoan_Enc,
                CMNDHash = model.CMNDHash,
                SotaikhoanHash = model.SotaikhoanHash
            };
        }

        public NhanVienCryptoModel HashNhanVienHmacSha256(NhanVienCryptoModel model)
        {
            if (model == null) return null;
            return new NhanVienCryptoModel
            {
                Id = model.Id,
                I_Holot = model.I_Holot,
                I_Ten = model.I_Ten,
                I_CMND = model.I_CMND,
                I_Sotaikhoan = model.I_Sotaikhoan,
                Holot_Enc = model.Holot_Enc,
                Ten_Enc = model.Ten_Enc,
                CMND_Enc = model.CMND_Enc,
                Sotaikhoan_Enc = model.Sotaikhoan_Enc,
                CMNDHash = HashHmacSha256(model.I_CMND),
                SotaikhoanHash = HashHmacSha256(model.I_Sotaikhoan)
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

        private byte[] ResolveRequiredBase64Key(string configKey, int expectedSize)
        {
            byte[] key = ResolveRequiredBase64Value(configKey);
            if (key.Length != expectedSize)
            {
                throw new InvalidOperationException($"Cau hinh {configKey} phai dai {expectedSize} bytes ({expectedSize * 8} bit). Hay cap nhat {ToEnvironmentKey(configKey)} trong file .env.");
            }

            return key;
        }

        private byte[] ResolveTweak()
        {
            return ResolveRequiredBase64Value("Encryption:FpeTweak");
        }

        private byte[] ResolveRequiredBase64Value(string configKey)
        {
            string configured = _configuration.GetValue<string>(configKey);
            if (string.IsNullOrWhiteSpace(configured))
            {
                throw new InvalidOperationException($"Thieu cau hinh {configKey}. Hay them {ToEnvironmentKey(configKey)} vao file .env.");
            }

            try
            {
                byte[] value = Convert.FromBase64String(configured);
                if (value.Length == 0)
                {
                    throw new InvalidOperationException($"Cau hinh {configKey} khong duoc rong.");
                }

                return value;
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException($"Cau hinh {configKey} phai la chuoi Base64 hop le.", ex);
            }
        }

        private static string ToEnvironmentKey(string configKey)
        {
            return configKey.Replace(":", "__");
        }

        private RSA CreateRsaForEncryption()
        {
            string publicKey = _configuration.GetValue<string>("Encryption:RsaPublicKey");
            if (string.IsNullOrWhiteSpace(publicKey))
            {
                throw new InvalidOperationException("Thieu cau hinh Encryption:RsaPublicKey. Hay them Encryption__RsaPublicKey vao file .env.");
            }

            RSA rsa = RSA.Create();
            try
            {
                rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
                return rsa;
            }
            catch (FormatException ex)
            {
                rsa.Dispose();
                throw new InvalidOperationException("Cau hinh Encryption:RsaPublicKey phai la chuoi Base64 hop le.", ex);
            }
            catch (CryptographicException ex)
            {
                rsa.Dispose();
                throw new InvalidOperationException("Cau hinh Encryption:RsaPublicKey khong dung dinh dang SubjectPublicKeyInfo.", ex);
            }
        }

        private RSA CreateRsaForDecryption()
        {
            string privateKey = _configuration.GetValue<string>("Encryption:RsaPrivateKey");
            if (string.IsNullOrWhiteSpace(privateKey))
            {
                throw new InvalidOperationException("Thieu cau hinh Encryption:RsaPrivateKey. Hay them Encryption__RsaPrivateKey vao file .env.");
            }

            RSA rsa = RSA.Create();
            try
            {
                rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKey), out _);
                return rsa;
            }
            catch (FormatException ex)
            {
                rsa.Dispose();
                throw new InvalidOperationException("Cau hinh Encryption:RsaPrivateKey phai la chuoi Base64 hop le.", ex);
            }
            catch (CryptographicException ex)
            {
                rsa.Dispose();
                throw new InvalidOperationException("Cau hinh Encryption:RsaPrivateKey khong dung dinh dang PKCS8 private key.", ex);
            }
        }
    }
}
