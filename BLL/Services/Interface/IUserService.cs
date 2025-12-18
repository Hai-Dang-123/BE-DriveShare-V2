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
    }
}
