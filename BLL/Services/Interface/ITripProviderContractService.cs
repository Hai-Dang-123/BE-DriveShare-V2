using Common.DTOs;
using System;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface ITripProviderContractService
    {
        Task<ResponseDTO> CreateAsync(CreateTripProviderContractDTO dto);
        Task<ResponseDTO> SignAsync(SignContractDTO dto);
        Task<ResponseDTO> GetByIdAsync(Guid contractId);
        Task CreateAndAddContractAsync(Guid tripId, Guid ownerId, Guid providerId, decimal fare);

        Task<ResponseDTO> GetAllAsync(int pageNumber, int pageSize);

        Task<ResponseDTO> SendOTPToSignContract(Guid contractId);
    }
}
