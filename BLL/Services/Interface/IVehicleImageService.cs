using Common.DTOs;
using Microsoft.AspNetCore.Http;
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

        /// <summary>
        /// Upload ảnh xe lên Firebase và AddAsync vào UnitOfWork.
        /// KHÔNG SaveChanges.
        /// </summary>
        Task AddImagesToVehicleAsync(Guid vehicleId, Guid userId, List<VehicleImageInputDTO> imageDTOs);
    }
}
