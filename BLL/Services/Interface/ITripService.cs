using Common.DTOs;

namespace BLL.Services.Interface
{
    public interface ITripService
    {
        Task<ResponseDTO> CreateTripFromPostAsync(TripCreateFromPostDTO dto);
        Task<ResponseDTO> ChangeTripStatusAsync(ChangeTripStatusDTO dto);
        Task<ResponseDTO> GetAllTripByOwnerIdAsync(Guid ownerId);
        Task<ResponseDTO> GetAllTripByDriverIdAsync(Guid driverId);
        Task<ResponseDTO> GetTripByIdAsync(Guid tripId);

    }
}
