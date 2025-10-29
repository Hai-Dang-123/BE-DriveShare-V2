using Common.DTOs;
using System;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IVehicleService
    {
        Task<ResponseDTO> CreateVehicleAsync(VehicleCreateDTO dto);
        Task<ResponseDTO> UpdateVehicleAsync(VehicleUpdateDTO dto);
        Task<ResponseDTO> SoftDeleteVehicleAsync(Guid id);
        Task<ResponseDTO> GetAllVehiclesAsync();
        Task<ResponseDTO> GetVehicleByIdAsync(Guid id);
    }
}
