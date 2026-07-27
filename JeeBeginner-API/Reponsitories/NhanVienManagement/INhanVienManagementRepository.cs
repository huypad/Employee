using JeeBeginner.Models.Common;
using JeeBeginner.Models.NhanVienManagement;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JeeBeginner.Reponsitories.NhanVienManagement
{
    public interface INhanVienManagementRepository
    {
        Task<IEnumerable<NhanVienModel>> Get_DSNhanVien(string whereStr, string orderByStr, int page, int record);
        Task<int> CountNhanVien(string whereStr);
        Task<NhanVienModel> GetNhanVienById(int id);
        Task<ReturnSqlModel> CreateNhanVien(NhanVienModel model);
        Task<ReturnSqlModel> UpdateNhanVien(NhanVienModel model);
        Task<ReturnSqlModel> DeleteNhanVien(int id);
        Task<ReturnSqlModel> UpdateLock(int id);
        Task<ReturnSqlModel> UpdateUnLock(int id);
        Task<int> EncryptExistingNhanViens();
        Task<int> RebuildSearchIndexes(int batchSize);
        Task<IEnumerable<NhanVienModel>> SearchAllEncrypted(string plainKeyword, string hashedKeyword);
    }
}
