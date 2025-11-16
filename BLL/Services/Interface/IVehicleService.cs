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
        Task<ResponseDTO> GetAllAsync(int pageNumber = 1, int pageSize = 10);
        Task<ResponseDTO> GetByIdAsync(Guid id);

        Task<ResponseDTO> GetMyVehiclesAsync(int pageNumber, int pageSize);
        Task<ResponseDTO> GetMyActiveVehiclesAsync(int pageNumber, int pageSize);
    }
}
