using Common.DTOs;
using System;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IVehicleImageService
    {
        Task<ResponseDTO> CreateVehicleImageAsync(VehicleImageCreateDTO dto);
        Task<ResponseDTO> UpdateVehicleImageAsync(VehicleImageUpdateDTO dto);
        Task<ResponseDTO> SoftDeleteVehicleImageAsync(Guid imageId);
        Task<ResponseDTO> GetAllVehicleImagesAsync(Guid vehicleId);
        Task<ResponseDTO> GetVehicleImageByIdAsync(Guid imageId);
    }
}
