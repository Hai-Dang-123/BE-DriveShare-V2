using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IUserService
    {
        Task<ResponseDTO> GetMyProfileAsync();
       
        Task<ResponseDTO> GetAllAsync(
             int pageNumber,
             int pageSize,
             string search = null,
             string sortField = null,
             string sortDirection = "ASC"
         );
        Task<ResponseDTO> GetByIdAsync(Guid userId);
        Task<ResponseDTO> GetAllUserByRoleAsync(
            string roleName,
            int pageNumber,
            int pageSize,
            string search = null,
            string sortField = null,
            string sortDirection = "ASC");

        // 5. UPDATE PROFILE (Dùng chung cho cả Admin sửa User và User tự sửa)
        Task<ResponseDTO> UpdateProfileAsync(Guid userId, UpdateUserProfileDTO model);

        // 6. DELETE USER (Soft Delete)
        Task<ResponseDTO> DeleteUserAsync(Guid userId);
        // [NEW] Gửi yêu cầu kích hoạt lại tài khoản
        Task<ResponseDTO> RequestAccountActivationAsync();

        // [NEW] Admin duyệt yêu cầu kích hoạt
        Task<ResponseDTO> ApproveAccountActivationAsync(Guid userId, bool isApproved);

        Task<Guid> GetAdminUserIdAsync();
    }
}
