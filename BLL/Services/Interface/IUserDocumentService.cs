using Common.DTOs;
using System;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IUserDocumentService
    {

        /// <summary>
        /// Kiểm tra xem User hiện tại đã có CCCD trạng thái ACTIVE hay chưa
        /// </summary>
        Task<ResponseDTO> CheckCCCDVerifiedAsync();

        Task<ResponseDTO> GetAllAsync();
        Task<ResponseDTO> GetByIdAsync(Guid id);
        Task<ResponseDTO> GetByUserIdAsync(Guid userId);

    }
}
