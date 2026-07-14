using JeeBeginner.Classes;
using JeeBeginner.Models.Encryption;
using JeeBeginner.Services.Encryption;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System;

namespace JeeBeginner.Controllers
{
    [EnableCors("AllowOrigin")]
    [Route("api/encryptiontest")]
    [ApiController]
    public class EncryptionTestController : ControllerBase
    {
        private readonly IEncryptionService _encryptionService;

        public EncryptionTestController(IEncryptionService encryptionService)
        {
            _encryptionService = encryptionService;
        }

        [HttpPost("plaintext/field")]
        public object PlainTextField([FromBody] FieldCryptoRequest request)
        {
            return ProcessField(request, "PLAINTEXT", "READ", value => value);
        }

        [HttpPost("aes/field/encrypt")]
        public object EncryptAesField([FromBody] FieldCryptoRequest request)
        {
            return ProcessField(request, "AES", "ENCRYPT", _encryptionService.EncryptAes);
        }

        [HttpPost("aes/field/decrypt")]
        public object DecryptAesField([FromBody] FieldCryptoRequest request)
        {
            return ProcessField(request, "AES", "DECRYPT", _encryptionService.DecryptAes);
        }

        [HttpPost("rsa/field/encrypt")]
        public object EncryptRsaField([FromBody] FieldCryptoRequest request)
        {
            return ProcessField(request, "RSA", "ENCRYPT", _encryptionService.EncryptRsa);
        }

        [HttpPost("rsa/field/decrypt")]
        public object DecryptRsaField([FromBody] FieldCryptoRequest request)
        {
            return ProcessField(request, "RSA", "DECRYPT", _encryptionService.DecryptRsa);
        }

        [HttpPost("hmacsha256/field/hash")]
        public object HashHmacSha256Field([FromBody] FieldCryptoRequest request)
        {
            return ProcessField(request, "HMAC-SHA256", "HASH", _encryptionService.HashHmacSha256);
        }

        [HttpPost("fpe/field/encrypt")]
        public object EncryptFpeField([FromBody] FieldCryptoRequest request)
        {
            return ProcessFpeField(request, true);
        }

        [HttpPost("fpe/field/decrypt")]
        public object DecryptFpeField([FromBody] FieldCryptoRequest request)
        {
            return ProcessFpeField(request, false);
        }

        [HttpPost("plaintext/nhanvien")]
        public object PlainTextNhanVien([FromBody] NhanVienCryptoModel request)
        {
            return ProcessNhanVien(request, "PLAINTEXT", "READ", model => model);
        }

        [HttpPost("aes/nhanvien/encrypt")]
        public object EncryptAesNhanVien([FromBody] NhanVienCryptoModel request)
        {
            return ProcessNhanVien(request, "AES", "ENCRYPT", _encryptionService.EncryptNhanVienAes);
        }

        [HttpPost("aes/nhanvien/decrypt")]
        public object DecryptAesNhanVien([FromBody] NhanVienCryptoModel request)
        {
            return ProcessNhanVien(request, "AES", "DECRYPT", _encryptionService.DecryptNhanVienAes);
        }

        [HttpPost("rsa/nhanvien/encrypt")]
        public object EncryptRsaNhanVien([FromBody] NhanVienCryptoModel request)
        {
            return ProcessNhanVien(request, "RSA", "ENCRYPT", _encryptionService.EncryptNhanVienRsa);
        }

        [HttpPost("rsa/nhanvien/decrypt")]
        public object DecryptRsaNhanVien([FromBody] NhanVienCryptoModel request)
        {
            return ProcessNhanVien(request, "RSA", "DECRYPT", _encryptionService.DecryptNhanVienRsa);
        }

        [HttpPost("fpe/nhanvien/encrypt")]
        public object EncryptFpeNhanVien([FromBody] NhanVienCryptoModel request)
        {
            return ProcessNhanVien(request, "FPE", "ENCRYPT", _encryptionService.EncryptNhanVienFpe);
        }

        [HttpPost("fpe/nhanvien/decrypt")]
        public object DecryptFpeNhanVien([FromBody] NhanVienCryptoModel request)
        {
            return ProcessNhanVien(request, "FPE", "DECRYPT", _encryptionService.DecryptNhanVienFpe);
        }

        
        [HttpPost("hmacsha256/nhanvien/hash")]
        public object HashHmacSha256NhanVien([FromBody] NhanVienCryptoModel request)
        {
            return ProcessNhanVien(request, "HMAC-SHA256", "HASH", _encryptionService.HashNhanVienHmacSha256);
        }

        private object ProcessField(FieldCryptoRequest request, string algorithm, string operation, Func<string, string> handler)
        {
            try
            {
                if (request == null) return JsonResultCommon.BatBuoc("request");

                string output = handler(request.Value);

                return JsonResultCommon.ThanhCong(new FieldCryptoResponse
                {
                    FieldName = request.FieldName,
                    Algorithm = algorithm,
                    Operation = operation,
                    InputValue = request.Value,
                    OutputValue = output
                });
            }
            catch (Exception ex)
            {
                return JsonResultCommon.Exception(ex);
            }
        }

        private object ProcessFpeField(FieldCryptoRequest request, bool encrypt)
        {
            Func<string, string> handler = IsDigitField(request?.FieldName)
                ? (encrypt ? _encryptionService.EncryptFpeDigits : _encryptionService.DecryptFpeDigits)
                : (encrypt ? _encryptionService.EncryptFpeAlphaNumeric : _encryptionService.DecryptFpeAlphaNumeric);

            return ProcessField(request, "FPE", encrypt ? "ENCRYPT" : "DECRYPT", handler);
        }

        private object ProcessNhanVien(NhanVienCryptoModel request, string algorithm, string operation, Func<NhanVienCryptoModel, NhanVienCryptoModel> handler)
        {
            try
            {
                if (request == null) return JsonResultCommon.BatBuoc("nhan vien");

                NhanVienCryptoModel output = handler(request);

                return JsonResultCommon.ThanhCong(new NhanVienCryptoResponse
                {
                    Algorithm = algorithm,
                    Operation = operation,
                    Data = output
                });
            }
            catch (Exception ex)
            {
                return JsonResultCommon.Exception(ex);
            }
        }

        private bool IsDigitField(string fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName)) return false;
            return fieldName.Equals("CMND", StringComparison.OrdinalIgnoreCase)
                || fieldName.Equals("I_CMND", StringComparison.OrdinalIgnoreCase)
                || fieldName.Equals("CMND_Enc", StringComparison.OrdinalIgnoreCase)
                || fieldName.Equals("Sotaikhoan", StringComparison.OrdinalIgnoreCase)
                || fieldName.Equals("I_Sotaikhoan", StringComparison.OrdinalIgnoreCase)
                || fieldName.Equals("Sotaikhoan_Enc", StringComparison.OrdinalIgnoreCase);
        }
    }
}
