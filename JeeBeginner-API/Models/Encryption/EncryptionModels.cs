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
        public double ServerExecutionTimeMs { get; set; }
    }

    public class NhanVienCryptoModel
    {
        public int Id { get; set; }
        public string I_Holot { get; set; }
        public string I_Ten { get; set; }
        public string I_CMND { get; set; }
        public string I_Sotaikhoan { get; set; }
        public string Holot_Enc { get; set; }
        public string Ten_Enc { get; set; }
        public string CMND_Enc { get; set; }
        public string CMND_FPE { get; set; }
        public string Sotaikhoan_Enc { get; set; }
        public string CMNDHash { get; set; }
        public string SotaikhoanHash { get; set; }
    }
    public class NhanVienCryptoResponse
    {
        public string Algorithm { get; set; }
        public string Operation { get; set; }
        public NhanVienCryptoModel Data { get; set; }
    }
}
