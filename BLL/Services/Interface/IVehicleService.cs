using Common.DTOs;
using System;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IVehicleService
    {
        Task<ResponseDTO> CreateAsync(VehicleCreateDTO dto);
        Task<ResponseDTO> UpdateAsync(VehicleUpdateDTO dto);
        Task<ResponseDTO> SoftDeleteAsync(Guid id);

        Task<ResponseDTO> GetByIdAsync(Guid id);


        // 1. Admin/Public Get All
        Task<ResponseDTO> GetAllAsync(int pageNumber, int pageSize, string? search, string? sortBy, string? sortOrder);

        // 2. Owner Get My Vehicles
        Task<ResponseDTO> GetMyVehiclesAsync(int pageNumber, int pageSize, string? search, string? sortBy, string? sortOrder);

        // 3. Owner Get My Active Vehicles (Chỉ lấy xe đang hoạt động để chọn chạy)
        Task<ResponseDTO> GetMyActiveVehiclesAsync(int pageNumber, int pageSize, string? search, string? sortBy, string? sortOrder);
    }
}
