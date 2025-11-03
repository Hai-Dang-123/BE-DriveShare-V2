using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IDeliveryRecordTermService
    {
        Task<ResponseDTO> CreateAsync(DeliveryRecordTermDTO dto);
        Task<ResponseDTO> UpdateAsync(Guid id, DeliveryRecordTermDTO dto);
        Task<ResponseDTO> DeleteAsync(Guid id);
        Task<ResponseDTO> GetAllAsync();
        Task<ResponseDTO> GetByIdAsync(Guid id);
    }
}