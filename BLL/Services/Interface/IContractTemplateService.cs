using Common.DTOs;
using Common.Enums.Type;
using System;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IContractTemplateService
    {
        Task<ResponseDTO> CreateAsync(ContractTemplateCreateDTO dto);
        Task<ResponseDTO> UpdateAsync(ContractTemplateUpdateDTO dto);
        Task<ResponseDTO> SoftDeleteAsync(Guid id);
        Task<ResponseDTO> GetAllAsync();
        Task<ResponseDTO> GetByIdAsync(Guid id);

        Task<ResponseDTO> GetLatestByTypeAsync(ContractType type);
    }
}
