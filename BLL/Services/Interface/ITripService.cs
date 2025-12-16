using Common.DTOs;

namespace BLL.Services.Interface
{
    public interface ITripService
    {
        Task<ResponseDTO> CreateTripFromPostAsync(TripCreateFromPostDTO dto);
        Task<ResponseDTO> ChangeTripStatusAsync(ChangeTripStatusDTO dto);
        // 1. Admin Get All
        Task<ResponseDTO> GetAllAsync(int pageNumber, int pageSize, string? search, string? sortField, string? sortDirection);

        // 2. Owner Get My Trips
        Task<ResponseDTO> GetAllTripsByOwnerAsync(int pageNumber, int pageSize, string? search, string? sortField, string? sortDirection);

        // 3. Driver Get My Trips
        Task<ResponseDTO> GetAllTripsByDriverAsync(int pageNumber, int pageSize, string? search, string? sortField, string? sortDirection);

        // 4. Provider Get My Trips
        Task<ResponseDTO> GetAllTripsByProviderAsync(int pageNumber, int pageSize, string? search, string? sortField, string? sortDirection);
        Task<ResponseDTO> GetTripByIdAsync(Guid tripId);

        Task<ResponseDTO> GetTripDriverAnalysisAsync(Guid tripId);

        Task<ResponseDTO> CancelTripByOwnerAsync(CancelTripDTO dto);





    }
}
