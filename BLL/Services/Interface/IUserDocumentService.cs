using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IUserDocumentService
    {
        Task<ResponseDTO> CreateAsync(UserDocumentDTO dto);
        Task<ResponseDTO> UpdateAsync(Guid id, UserDocumentDTO dto);
        Task<ResponseDTO> DeleteAsync(Guid id);
        Task<ResponseDTO> GetAllAsync();
        Task<ResponseDTO> GetByIdAsync(Guid id);
    }
}