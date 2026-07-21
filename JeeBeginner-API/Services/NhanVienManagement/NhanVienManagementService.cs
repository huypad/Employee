using JeeBeginner.Models.Common;
using JeeBeginner.Models.NhanVienManagement;
using JeeBeginner.Reponsitories.NhanVienManagement;
using JeeBeginner.Services.Encryption;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JeeBeginner.Services.NhanVienManagement
{
    public class NhanVienManagementService : INhanVienManagementService
    {
        private readonly INhanVienManagementRepository _repository;
        private readonly IEncryptionService _encryptionService;

        public NhanVienManagementService(INhanVienManagementRepository repository, IEncryptionService encryptionService)
        {
            _repository = repository;
            _encryptionService = encryptionService;
        }
        public Task<IEnumerable<NhanVienModel>> Get_DSNhanVien(string whereStr, string orderByStr) => _repository.Get_DSNhanVien(whereStr, orderByStr);
        public Task<NhanVienModel> GetNhanVienById(int id) => _repository.GetNhanVienById(id);
        public Task<ReturnSqlModel> CreateNhanVien(NhanVienModel model) => _repository.CreateNhanVien(model);
        public Task<ReturnSqlModel> UpdateNhanVien(NhanVienModel model) => _repository.UpdateNhanVien(model);
        public Task<ReturnSqlModel> DeleteNhanVien(int id) => _repository.DeleteNhanVien(id);
        public Task<ReturnSqlModel> UpdateLock(int id) => _repository.UpdateLock(id);
        public Task<ReturnSqlModel> UpdateUnLock(int id) => _repository.UpdateUnLock(id);
        public Task<int> EncryptExistingNhanViens() => _repository.EncryptExistingNhanViens();

        
        public async Task<IEnumerable<NhanVienModel>> SearchNhanVien(string keyword)
        {
           
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return new List<NhanVienModel>();
            }


            string hashedKeyword = _encryptionService.HashSearchIndex(keyword);

           
            return await _repository.SearchAllEncrypted(keyword, hashedKeyword);
        }
    }
}
