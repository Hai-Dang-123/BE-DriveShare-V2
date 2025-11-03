using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IVehicleDocumentService
    {
        Task<ResponseDTO> CreateAsync(VehicleDocumentDTO dto);
        Task<ResponseDTO> UpdateAsync(Guid id, VehicleDocumentDTO dto);
        Task<ResponseDTO> DeleteAsync(Guid id);
        Task<ResponseDTO> GetAllAsync();
        Task<ResponseDTO> GetByIdAsync(Guid id);
    }
}