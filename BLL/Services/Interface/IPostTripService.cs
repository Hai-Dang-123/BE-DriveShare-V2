using Common.DTOs;
using Common.Enums.Status;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IPostTripService
    {
        Task<ResponseDTO> CreatePostTripAsync(PostTripCreateDTO dto);

        Task<ResponseDTO> GetAllOpenPostTripsAsync(int pageNumber, int pageSize);

        Task<ResponseDTO> GetMyPostTripsAsync(int pageNumber, int pageSize);
        Task<ResponseDTO> GetPostTripByIdAsync(Guid postTripId);
        Task<ResponseDTO> ChangePostTripStatusAsync(Guid postTripId, PostStatus newStatus);
    }
}
