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
    }
}
