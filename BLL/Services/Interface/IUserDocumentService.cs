using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IUserDocumentService
    {
        /// <summary>
        /// Kiểm tra xem User hiện tại đã có CCCD trạng thái ACTIVE hay chưa
        /// </summary>
        Task<ResponseDTO> CheckCCCDVerifiedAsync();
    }
}