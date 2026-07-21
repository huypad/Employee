using JeeBeginner.Models.Common;
using JeeBeginner.Models.NhanVienManagement;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JeeBeginner.Services.NhanVienManagement
{
    public interface INhanVienManagementService
    {
        Task<IEnumerable<NhanVienModel>> Get_DSNhanVien(string whereStr, string orderByStr);
        Task<NhanVienModel> GetNhanVienById(int id);
        Task<ReturnSqlModel> CreateNhanVien(NhanVienModel model);
        Task<ReturnSqlModel> UpdateNhanVien(NhanVienModel model);
        Task<ReturnSqlModel> DeleteNhanVien(int id);
        Task<ReturnSqlModel> UpdateLock(int id);
        Task<ReturnSqlModel> UpdateUnLock(int id);
        Task<int> EncryptExistingNhanViens();

       
        Task<IEnumerable<NhanVienModel>> SearchNhanVien(string keyword);
    }
}
