using BLL.Services.Interface;
using BLL.Utilities;
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
        private readonly UserUtility _userUtility;

        public VehicleDocumentService(IUnitOfWork unitOfWork, IFirebaseUploadService firebaseService, UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _firebaseService = firebaseService;
            _userUtility = userUtility;
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
        public async Task<ResponseDTO> AddDocumentAsync( Guid vehicleId, AddVehicleDocumentDTO dto)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                {
                    return new ResponseDTO("Người dùng không hợp lệ.", 401, false);
                }
                // 1. Kiểm tra xe và quyền sở hữu
                var vehicle = await _unitOfWork.VehicleRepo.GetByIdAsync(vehicleId);

                if (vehicle == null)
                {
                    return new ResponseDTO("Không tìm thấy thông tin xe.", 404, false);
                }

                if (vehicle.OwnerId != userId)
                {
                    return new ResponseDTO("Bạn không có quyền thêm giấy tờ cho xe này.", 403, false);
                }

                // 2. Validate dữ liệu
                if (dto.FrontFile == null || dto.FrontFile.Length == 0)
                {
                    return new ResponseDTO("Vui lòng tải lên ảnh mặt trước.", 400, false);
                }

                // 3. Upload ảnh lên Firebase (Chạy song song cho nhanh)
                var uploadTasks = new List<Task<string>>();

                // Upload mặt trước
                var frontTask = _firebaseService.UploadFileAsync(dto.FrontFile, userId, FirebaseFileType.VEHICLE_DOCUMENTS);
                uploadTasks.Add(frontTask);

                // Upload mặt sau (nếu có)
                Task<string>? backTask = null;
                if (dto.BackFile != null && dto.BackFile.Length > 0)
                {
                    backTask = _firebaseService.UploadFileAsync(dto.BackFile, userId, FirebaseFileType.VEHICLE_DOCUMENTS);
                    uploadTasks.Add(backTask!);
                }

                // Chờ tất cả upload xong
                await Task.WhenAll(uploadTasks);

                // 4. Tạo Entity
                var document = new VehicleDocument
                {
                    VehicleDocumentId = Guid.NewGuid(),
                    VehicleId = vehicleId,
                    DocumentType = dto.DocumentType, // Loại giấy tờ (Cà vẹt, Bảo hiểm...)
                    ExpirationDate = dto.ExpirationDate,

                    FrontDocumentUrl = await frontTask, // Lấy kết quả từ Task
                    BackDocumentUrl = backTask != null ? await backTask : null,

                    Status = VerifileStatus.INACTIVE, // Mặc định chờ duyệt
                    CreatedAt = DateTime.UtcNow
                };

                // 5. Lưu vào DB
                await _unitOfWork.VehicleDocumentRepo.AddAsync(document);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Thêm giấy tờ thành công. Vui lòng chờ hệ thống duyệt.", 201, true, document);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi hệ thống: {ex.Message}", 500, false);
            }
        }
    }
}
