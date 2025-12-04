using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IVehicleDocumentService
    {
        /// <summary>
        /// Upload file giấy tờ (mặt trước/sau) và AddAsync vào UnitOfWork.
        /// KHÔNG SaveChanges.
        /// </summary>
        Task AddDocumentsToVehicleAsync(Guid vehicleId, Guid userId, List<VehicleDocumentInputDTO> documentDTOs);
        Task<ResponseDTO> AddDocumentAsync(Guid vehicleId, AddVehicleDocumentDTO dto);

        // 1. Lấy danh sách tóm tắt (Có phân trang)
        // [UPDATED] Thêm tham số search và sort
        Task<ResponseDTO> GetPendingVehicleDocumentsListAsync(
            int pageNumber,
            int pageSize,
            string? search = null,
            string? sortField = null,
            string? sortOrder = "DESC");

        // 2. Lấy chi tiết 1 giấy tờ
        Task<ResponseDTO> GetVehicleDocumentDetailAsync(Guid documentId);

        // 3. Duyệt
        Task<ResponseDTO> ReviewVehicleDocumentAsync(Guid documentId, bool isApproved, string? rejectReason);
    }
}
