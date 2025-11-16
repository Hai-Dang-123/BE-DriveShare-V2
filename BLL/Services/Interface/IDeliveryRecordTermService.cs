using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IDeliveryRecordTermService
    {
        // Thêm các tham số phân trang
        Task<ResponseDTO> GetAllAsync(int pageNumber, int pageSize);

        // (Optional: Thêm hàm này rất hữu ích)
        Task<ResponseDTO> GetAllByTemplateIdAsync(Guid templateId, int pageNumber, int pageSize);

        Task<ResponseDTO> GetByIdAsync(Guid id);
        Task<ResponseDTO> CreateAsync(DeliveryRecordTermDTO dto);
        Task<ResponseDTO> UpdateAsync(Guid id, DeliveryRecordTermDTO dto);
        Task<ResponseDTO> DeleteAsync(Guid id);
    }
}
