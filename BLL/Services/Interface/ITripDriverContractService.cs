using Common.DTOs;
using DAL.Entities;
using System;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface ITripDriverContractService
    {
        Task<ResponseDTO> CreateAsync(CreateTripDriverContractDTO dto);
        Task<ResponseDTO> SignAsync(SignContractDTO dto);
        Task<ResponseDTO> GetByIdAsync(Guid contractId);
        Task<TripDriverContract> CreateContractInternalAsync(Guid tripId, Guid ownerId, Guid driverId, decimal? fare);

        Task<ResponseDTO> GetAllAsync(int pageNumber, int pageSize);
    }
}
