using Common.DTOs;
using Common.Enums.Type;
using DAL.Entities;
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

        // Sửa hàm này:
        Task<ResponseDTO> GetAllAsync(int pageNumber, int pageSize);

        Task<ResponseDTO> GetByIdAsync(Guid id);
        Task<DeliveryRecordTemplate> GetLatestTemplateByTypeAsync(DeliveryRecordType type);
        Task<ResponseDTO> GetLatestDeliveryRecordTemplateByTypeAsync(DeliveryRecordType type);
    }
}