using Common.DTOs;
using Common.Enums.Status;
using System;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IPostTripService
    {
        // 1. CREATE
        Task<ResponseDTO> CreatePostTripAsync(PostTripCreateDTO dto);

        // 2. GET ALL OPEN (public)
        Task<ResponseDTO> GetAllOpenPostTripsAsync(int pageNumber, int pageSize);

        // 3. GET ALL (Admin / Public) — Search + Sort + Paging  🔥 NEW
        Task<ResponseDTO> GetAllPostTripsAsync(
            int pageNumber,
            int pageSize,
            string? search = null,
            string? sortField = null,
            string? sortDirection = "ASC"
        );

        // 4. GET MY POST TRIPS (Owner)
        Task<ResponseDTO> GetMyPostTripsAsync(int pageNumber, int pageSize);

        // 5. GET BY ID
        Task<ResponseDTO> GetPostTripByIdAsync(Guid postTripId);
        Task<ResponseDTO> ChangePostTripStatusAsync(Guid postTripId, PostStatus newStatus);
    }
}
