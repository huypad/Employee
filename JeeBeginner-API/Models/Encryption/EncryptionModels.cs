namespace JeeBeginner.Models.Encryption
{
    public class FieldCryptoRequest
    {
        public string FieldName { get; set; }
        public string Value { get; set; }
    }

    public class FieldCryptoResponse
    {
        public string FieldName { get; set; }
        public string Algorithm { get; set; }
        public string Operation { get; set; }
        public string InputValue { get; set; }
        public string OutputValue { get; set; }
    }

    public class NhanVienCryptoModel
    {
        public int Id { get; set; }
        public string MaNV { get; set; }
        public string HoTen { get; set; }
        public string SDT { get; set; }
        public string CCCD { get; set; }
        public string Email { get; set; }
        public string DiaChi { get; set; }
        public string PhongBan { get; set; }
        public string ChucVu { get; set; }
    }

    public class NhanVienCryptoResponse
    {
        public string Algorithm { get; set; }
        public string Operation { get; set; }
        public NhanVienCryptoModel Data { get; set; }
    }
}
