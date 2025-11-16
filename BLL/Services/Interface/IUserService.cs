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
        /// <summary>
        /// [Admin] Lấy danh sách tất cả người dùng (phân trang).
        /// </summary>
        Task<ResponseDTO> GetAllAsync(int pageNumber, int pageSize);

        /// <summary>
        /// [Admin] Lấy thông tin chi tiết của một người dùng bất kỳ.
        /// </summary>
        Task<ResponseDTO> GetByIdAsync(Guid userId);
    }
}
