using BLL.Services.Interface;
using Common.DTOs;
using Common.Enums.Status;
using Common.Settings;
using DAL.Entities;
using DAL.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class VehicleDocumentService : IVehicleDocumentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFirebaseUploadService _firebaseService;

        public VehicleDocumentService(IUnitOfWork unitOfWork, IFirebaseUploadService firebaseService)
        {
            _unitOfWork = unitOfWork;
            _firebaseService = firebaseService;
        }

        public async Task AddDocumentsToVehicleAsync(Guid vehicleId, Guid userId, List<VehicleDocumentInputDTO> documentDTOs)
        {
            if (documentDTOs == null || !documentDTOs.Any()) return;

            foreach (var dto in documentDTOs)
            {
                // 1. Upload song song 2 mặt
                var frontTask = _firebaseService.UploadFileAsync(dto.FrontFile, userId, FirebaseFileType.VEHICLE_DOCUMENTS);
                var backTask = _firebaseService.UploadFileAsync(dto.BackFile, userId, FirebaseFileType.VEHICLE_DOCUMENTS);

                await Task.WhenAll(frontTask, backTask);

                // 2. Tạo entity
                var document = new VehicleDocument
                {
                    VehicleDocumentId = Guid.NewGuid(),
                    VehicleId = vehicleId,
                    DocumentType = Common.Enums.Type.DocumentType.VEHICLE_LINCENSE,
                    ExpirationDate = dto.ExpirationDate,
                    FrontDocumentUrl = await frontTask,
                    BackDocumentUrl = await backTask,
                    Status = VerifileStatus.INACTIVE, // Mặc định
                    CreatedAt = DateTime.UtcNow,
                    // Hash và RawResult để null như yêu cầu
                };

                // 3. Add vào Repo
                await _unitOfWork.VehicleDocumentRepo.AddAsync(document);
            }
        }
    }
}
