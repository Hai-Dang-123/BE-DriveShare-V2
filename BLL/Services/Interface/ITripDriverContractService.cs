using Common.DTOs;
using DAL.Entities;
using System;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface ITripDriverContractService
    {
        Task<ResponseDTO> CreateAsync(CreateTripDriverContractDTO dto);
        Task<ResponseDTO> SignAsync(Guid contractId);
        Task<ResponseDTO> GetByIdAsync(Guid contractId);
        Task<TripDriverContract> CreateContractInternalAsync(CreateTripDriverContractDTO dto, Guid ownerId);
    }
}
