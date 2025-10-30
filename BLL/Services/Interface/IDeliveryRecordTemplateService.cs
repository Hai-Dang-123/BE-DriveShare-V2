using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IDeliveryRecordTemplateService
    {
        Task<ResponseDTO> CreateAsync(DeliveryRecordTemplateDTO dto);
        Task<ResponseDTO> UpdateAsync(Guid id, DeliveryRecordTemplateDTO dto);
        Task<ResponseDTO> DeleteAsync(Guid id);
        Task<ResponseDTO> GetAllAsync();
        Task<ResponseDTO> GetByIdAsync(Guid id);
    }
}