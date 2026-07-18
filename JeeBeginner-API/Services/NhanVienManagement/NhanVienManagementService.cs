using JeeBeginner.Models.Common;
using JeeBeginner.Models.NhanVienManagement;
using JeeBeginner.Reponsitories.NhanVienManagement;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JeeBeginner.Services.NhanVienManagement
{
    public class NhanVienManagementService : INhanVienManagementService
    {
        private readonly INhanVienManagementRepository _repository;

        public NhanVienManagementService(INhanVienManagementRepository repository) => _repository = repository;
        public Task<IEnumerable<NhanVienModel>> Get_DSNhanVien(string whereStr, string orderByStr) => _repository.Get_DSNhanVien(whereStr, orderByStr);
        public Task<NhanVienModel> GetNhanVienById(int id) => _repository.GetNhanVienById(id);
        public Task<ReturnSqlModel> CreateNhanVien(NhanVienModel model) => _repository.CreateNhanVien(model);
        public Task<ReturnSqlModel> UpdateNhanVien(NhanVienModel model) => _repository.UpdateNhanVien(model);
        public Task<ReturnSqlModel> DeleteNhanVien(int id) => _repository.DeleteNhanVien(id);
        public Task<ReturnSqlModel> UpdateLock(int id) => _repository.UpdateLock(id);
        public Task<ReturnSqlModel> UpdateUnLock(int id) => _repository.UpdateUnLock(id);
        public Task<int> EncryptExistingNhanViens() => _repository.EncryptExistingNhanViens();

        public Task<IEnumerable<NhanVienModel>> SearchAllEncrypted(string plainKeyword, string hashedKeyword)
        {
            return _repository.SearchAllEncrypted(plainKeyword, hashedKeyword);
        }
    }
}
