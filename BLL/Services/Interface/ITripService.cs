using Common.DTOs;

namespace BLL.Services.Interface
{
    public interface ITripService
    {
        Task<ResponseDTO> CreateTripFromPostAsync(TripCreateFromPostDTO dto);
        Task<ResponseDTO> ChangeTripStatusAsync(ChangeTripStatusDTO dto);
        Task<ResponseDTO> GetAllTripsByDriverAsync(int pageNumber = 1, int pageSize = 10);
        Task<ResponseDTO> GetAllTripsByOwnerAsync(int pageNumber = 1, int pageSize = 10);
        Task<ResponseDTO> GetAllTripsByProviderAsync(int pageNumber = 1, int pageSize = 10);
        Task<ResponseDTO> GetTripByIdAsync(Guid tripId);

    }
}
