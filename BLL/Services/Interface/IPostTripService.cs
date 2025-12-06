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

        // 1. Admin/Public Get All
        Task<ResponseDTO> GetAllPostTripsAsync(int pageNumber, int pageSize, string? search, string? sortField, string? sortDirection);

        // 2. Public Get Open
        Task<ResponseDTO> GetAllOpenPostTripsAsync(int pageNumber, int pageSize, string? search, string? sortField, string? sortDirection);

        // 3. Owner Get My Posts
        Task<ResponseDTO> GetMyPostTripsAsync(int pageNumber, int pageSize, string? search, string? sortField, string? sortDirection);

        // 5. GET BY ID
        Task<ResponseDTO> GetPostTripByIdAsync(Guid postTripId);
        Task<ResponseDTO> ChangePostTripStatusAsync(Guid postTripId, PostStatus newStatus);
    }
}
