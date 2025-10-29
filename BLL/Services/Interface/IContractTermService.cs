using Common.DTOs;
using System;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IContractTermService
    {
        Task<ResponseDTO> CreateAsync(ContractTermCreateDTO dto);
        Task<ResponseDTO> UpdateAsync(ContractTermUpdateDTO dto);
        Task<ResponseDTO> SoftDeleteAsync(Guid id);
        Task<ResponseDTO> GetAllAsync(Guid contractTemplateId);
        Task<ResponseDTO> GetByIdAsync(Guid id);
    }
}
