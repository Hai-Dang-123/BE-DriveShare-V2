using Common.DTOs;
using System;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IVehicleTypeService
    {
        Task<ResponseDTO> CreateVehicleTypeAsync(VehicleTypeCreateDTO dto);
        Task<ResponseDTO> UpdateVehicleTypeAsync(VehicleTypeUpdateDTO dto);
        Task<ResponseDTO> SoftDeleteVehicleTypeAsync(Guid id);
        Task<ResponseDTO> GetAllVehicleTypesAsync();
        Task<ResponseDTO> GetVehicleTypeByIdAsync(Guid id);
    }
}
