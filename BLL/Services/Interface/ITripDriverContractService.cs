using System;
using System.Threading.Tasks;
using Common.DTOs;

namespace BLL.Services.Interface
{
    public interface ITripDriverContractService
    {
        Task<ResponseDTO> CreateAsync(CreateTripDriverContractDTO dto);
        Task<ResponseDTO> SignAsync(Guid contractId);
        Task<ResponseDTO> GetByIdAsync(Guid contractId);
    }
}
