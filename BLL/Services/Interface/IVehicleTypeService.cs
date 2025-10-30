using Common.DTOs;
using System;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IVehicleTypeService
    {
        Task<ResponseDTO> CreateAsync(VehicleTypeCreateDTO dto);
        Task<ResponseDTO> UpdateAsync(VehicleTypeUpdateDTO dto);
        Task<ResponseDTO> SoftDeleteAsync(Guid id);
        Task<ResponseDTO> GetAllAsync();
        Task<ResponseDTO> GetByIdAsync(Guid id);
    }
}
