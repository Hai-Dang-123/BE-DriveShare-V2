using Common.DTOs;
using System;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IVehicleImageService
    {
        Task<ResponseDTO> CreateAsync(VehicleImageCreateDTO dto);
        Task<ResponseDTO> UpdateAsync(VehicleImageUpdateDTO dto);
        Task<ResponseDTO> SoftDeleteAsync(Guid imageId);
        Task<ResponseDTO> GetAllAsync(Guid vehicleId);
        Task<ResponseDTO> GetByIdAsync(Guid imageId);
    }
}
