using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Settings;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore; // Cần thiết
using System;
using System.Collections.Generic;
using System.Linq;
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

        // =========================================================================
        // 1. ADD DOCUMENTS (ĐƯỢC GỌI TỪ VEHICLE SERVICE KHI TẠO XE)
        // =========================================================================
        public async Task AddDocumentsToVehicleAsync(Guid vehicleId, Guid userId, List<VehicleDocumentInputDTO> documentDTOs)
        {
            if (documentDTOs == null || !documentDTOs.Any()) return;

            foreach (var dto in documentDTOs)
            {
                // 1. Upload song song 2 mặt
                var uploadTasks = new List<Task<string>>();
                var frontTask = _firebaseService.UploadFileAsync(dto.FrontFile, userId, FirebaseFileType.VEHICLE_DOCUMENTS);
                uploadTasks.Add(frontTask);

                Task<string>? backTask = null;
                if (dto.BackFile != null)
                {
                    backTask = _firebaseService.UploadFileAsync(dto.BackFile, userId, FirebaseFileType.VEHICLE_DOCUMENTS);
                    uploadTasks.Add(backTask!);
                }

                await Task.WhenAll(uploadTasks);

                // 2. Tạo entity
                var document = new VehicleDocument
                {
                    VehicleDocumentId = Guid.NewGuid(),
                    VehicleId = vehicleId,
                    DocumentType = Common.Enums.Type.DocumentType.VEHICLE_LINCENSE, // Mặc định là Cà vẹt
                    ExpirationDate = dto.ExpirationDate,
                    FrontDocumentUrl = await frontTask,
                    BackDocumentUrl = backTask != null ? await backTask : null,

                    // [UPDATED] Đặt trạng thái chờ duyệt
                    Status = VerifileStatus.PENDING_REVIEW,
                    CreatedAt = DateTime.UtcNow,
                };

                // 3. Add vào Repo (Chưa SaveChange vì VehicleService sẽ Save)
                await _unitOfWork.VehicleDocumentRepo.AddAsync(document);
            }
        }

        // =========================================================================
        // 2. ADD SINGLE DOCUMENT (API RIÊNG LẺ)
        // =========================================================================
        public async Task<ResponseDTO> AddDocumentAsync(Guid vehicleId, AddVehicleDocumentDTO dto)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized.", 401, false);

                // 1. Kiểm tra xe
                var vehicle = await _unitOfWork.VehicleRepo.GetByIdAsync(vehicleId);
                if (vehicle == null) return new ResponseDTO("Xe không tồn tại.", 404, false);
                if (vehicle.OwnerId != userId) return new ResponseDTO("Không có quyền.", 403, false);

                // 2. Validate
                if (dto.FrontFile == null || dto.FrontFile.Length == 0) return new ResponseDTO("Thiếu ảnh mặt trước.", 400, false);

                // 3. Upload Firebase
                var frontUrl = await _firebaseService.UploadFileAsync(dto.FrontFile, userId, FirebaseFileType.VEHICLE_DOCUMENTS);
                string? backUrl = null;
                if (dto.BackFile != null)
                {
                    backUrl = await _firebaseService.UploadFileAsync(dto.BackFile, userId, FirebaseFileType.VEHICLE_DOCUMENTS);
                }

                // 4. Tạo Entity
                var document = new VehicleDocument
                {
                    VehicleDocumentId = Guid.NewGuid(),
                    VehicleId = vehicleId,
                    DocumentType = dto.DocumentType,
                    ExpirationDate = dto.ExpirationDate,
                    FrontDocumentUrl = frontUrl,
                    BackDocumentUrl = backUrl,

                    // [UPDATED] Luôn là Pending Review khi mới tạo
                    Status = VerifileStatus.PENDING_REVIEW,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.VehicleDocumentRepo.AddAsync(document);
                await _unitOfWork.SaveChangeAsync();

                var response = new VehicleDocumentResponseDTO
                {
                    VehicleDocumentId = document.VehicleDocumentId,
                    VehicleId = document.VehicleId,
                    DocumentType = document.DocumentType.ToString(),
                    FrontImage = document.FrontDocumentUrl,
                    BackImage = document.BackDocumentUrl,
                    ExpirationDate = document.ExpirationDate,
                    Status = document.Status.ToString(),

                };

                return new ResponseDTO("Đã tải lên giấy tờ. Vui lòng chờ nhân viên duyệt.", 201, true, response);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi hệ thống: {ex.Message}", 500, false);
            }
        }

        // =========================================================================
        // 3. [NEW] STAFF DUYỆT GIẤY TỜ XE (MANUAL REVIEW)
        // =========================================================================
       
        public async Task<ResponseDTO> ReviewVehicleDocumentAsync(Guid documentId, bool isApproved, string? rejectReason)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync(); // Nên dùng Transaction để đảm bảo tính nhất quán
            try
            {
                // 1. Lấy thông tin giấy tờ
                var doc = await _unitOfWork.VehicleDocumentRepo.GetByIdAsync(documentId);
                if (doc == null) return new ResponseDTO("Giấy tờ không tồn tại.", 404, false);

                if (isApproved)
                {
                    // --- A. Cập nhật trạng thái giấy tờ ---
                    doc.Status = VerifileStatus.ACTIVE;
                    doc.ProcessedAt = DateTime.UtcNow;
                    doc.AdminNotes = "Approved by Staff.";

                    // --- B. [NEW] Cập nhật trạng thái XE ---
                    // Lấy thông tin xe dựa trên VehicleId của giấy tờ
                    var vehicle = await _unitOfWork.VehicleRepo.GetByIdAsync(doc.VehicleId);

                    if (vehicle != null)
                    {
                        // Cập nhật trạng thái xe thành ACTIVE (hoặc trạng thái tương đương trong Enum của bạn)
                        // Lưu ý: Bạn có thể cần kiểm tra xem xe có ĐỦ các loại giấy tờ bắt buộc chưa trước khi Active.
                        // Ở đây mình làm theo yêu cầu: duyệt là Active luôn.
                        vehicle.Status = VehicleStatus.ACTIVE;

                        await _unitOfWork.VehicleRepo.UpdateAsync(vehicle);
                    }
                }
                else
                {
                    // Từ chối giấy tờ
                    if (string.IsNullOrWhiteSpace(rejectReason))
                        return new ResponseDTO("Vui lòng nhập lý do từ chối.", 400, false);

                    doc.Status = VerifileStatus.REJECTED;
                    doc.ProcessedAt = DateTime.UtcNow;
                    doc.AdminNotes = rejectReason;

                    // (Optional) Nếu từ chối giấy tờ, bạn có muốn set Xe về trạng thái REJECTED hoặc PENDING không?
                    // Ví dụ:
                    
                    var vehicle = await _unitOfWork.VehicleRepo.GetByIdAsync(doc.VehicleId);
                    if (vehicle != null && vehicle.Status == VehicleStatus.ACTIVE)
                    {
                        vehicle.Status = VehicleStatus.INACTIVE; // Hoặc REJECTED
                        await _unitOfWork.VehicleRepo.UpdateAsync(vehicle);
                    }
                    
                }

                // Cập nhật giấy tờ
                await _unitOfWork.VehicleDocumentRepo.UpdateAsync(doc);

                // Lưu tất cả thay đổi (cả Doc và Vehicle)
                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                return new ResponseDTO(isApproved ? "Đã duyệt giấy tờ và kích hoạt xe." : "Đã từ chối giấy tờ xe.", 200, true);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO($"Lỗi: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> GetPendingVehicleDocumentsListAsync(
    int pageNumber,
    int pageSize,
    string? search = null,
    string? sortField = null,
    string? sortOrder = "DESC")
        {
            try
            {
                // 1. Base Query (Chỉ lấy PENDING_REVIEW, có Include để lấy thông tin Xe và Chủ xe)
                var query = _unitOfWork.VehicleDocumentRepo.GetAll()
                    .AsNoTracking()
                    .Include(d => d.Vehicle).ThenInclude(v => v.Owner)
                    .Where(d => d.Status == VerifileStatus.PENDING_REVIEW);

                // ======================================================
                // 2. SEARCH (Tìm theo Biển số, Tên chủ xe, Loại giấy tờ)
                // ======================================================
                if (!string.IsNullOrWhiteSpace(search))
                {
                    string keyword = search.Trim().ToLower();
                    query = query.Where(d =>
                        // Tìm theo Biển số
                        (d.Vehicle != null && d.Vehicle.PlateNumber.ToLower().Contains(keyword)) ||
                        // Tìm theo Tên chủ xe
                        (d.Vehicle != null && d.Vehicle.Owner != null && d.Vehicle.Owner.FullName.ToLower().Contains(keyword)) ||
                        // Tìm theo Loại giấy tờ
                        d.DocumentType.ToString().ToLower().Contains(keyword)
                    );
                }

                // ======================================================
                // 3. SORT (Sắp xếp động)
                // ======================================================
                // Các field hỗ trợ: "date" (ngày tạo), "plate" (biển số), "owner" (chủ xe), "type" (loại giấy)
                bool isDesc = (sortOrder?.ToUpper() == "DESC");

                query = sortField?.ToLower() switch
                {
                    "plate" => isDesc
                        ? query.OrderByDescending(d => d.Vehicle.PlateNumber)
                        : query.OrderBy(d => d.Vehicle.PlateNumber),

                    "owner" => isDesc
                        ? query.OrderByDescending(d => d.Vehicle.Owner.FullName)
                        : query.OrderBy(d => d.Vehicle.Owner.FullName),

                    "type" => isDesc
                        ? query.OrderByDescending(d => d.DocumentType)
                        : query.OrderBy(d => d.DocumentType),

                    "date" => isDesc
                        ? query.OrderByDescending(d => d.CreatedAt)
                        : query.OrderBy(d => d.CreatedAt),

                    // Mặc định: Mới nhất lên đầu
                    _ => query.OrderByDescending(d => d.CreatedAt)
                };

                // ======================================================
                // 4. PAGING & PROJECTION
                // ======================================================
                var totalCount = await query.CountAsync();

                var items = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(d => new VehicleDocumentPendingSummaryDTO
                    {
                        VehicleDocumentId = d.VehicleDocumentId,
                        DocumentType = d.DocumentType.ToString(),
                        // Null-safe check
                        VehiclePlate = d.Vehicle != null ? d.Vehicle.PlateNumber : "N/A",
                        OwnerName = (d.Vehicle != null && d.Vehicle.Owner != null) ? d.Vehicle.Owner.FullName : "N/A",
                        CreatedAt = d.CreatedAt
                    })
                    .ToListAsync();

                var paginatedResult = new PaginatedDTO<VehicleDocumentPendingSummaryDTO>(items, totalCount, pageNumber, pageSize);

                return new ResponseDTO("Lấy danh sách thành công.", 200, true, paginatedResult);
            }
            catch (Exception ex)
            {
                return new ResponseDTO(ex.Message, 500, false);
            }
        }

        // =========================================================================
        // B. LẤY CHI TIẾT ĐỂ DUYỆT (DETAIL)
        // =========================================================================
        public async Task<ResponseDTO> GetVehicleDocumentDetailAsync(Guid documentId)
        {
            try
            {
                
                var doc = await _unitOfWork.VehicleDocumentRepo.GetAll()
                    .AsNoTracking()
                    .Include(d => d.Vehicle).ThenInclude(v => v.Owner) // Include sâu để lấy thông tin đối chiếu
                    .FirstOrDefaultAsync(d => d.VehicleDocumentId == documentId);

                if (doc == null) return new ResponseDTO("Document not found", 404, false);

                var detail = new VehicleDocumentPendingDetailDTO
                {
                    VehicleDocumentId = doc.VehicleDocumentId,
                    DocumentType = doc.DocumentType.ToString(),
                    Status = doc.Status.ToString(),
                    AdminNotes = doc.AdminNotes,
                    ProcessedAt = doc.ProcessedAt,
                    CreatedAt = doc.CreatedAt,
                    ExpirationDate = doc.ExpirationDate,

                    FrontDocumentUrl = doc.FrontDocumentUrl,
                    BackDocumentUrl = doc.BackDocumentUrl,

                    VehiclePlate = doc.Vehicle?.PlateNumber ?? "N/A",
                    VehicleBrand = doc.Vehicle?.Brand ?? "N/A",
                    VehicleModel = doc.Vehicle?.Model ?? "N/A",
                    VehicleColor = doc.Vehicle?.Color ?? "N/A",
                    OwnerName = doc.Vehicle?.Owner?.FullName ?? "N/A",
                    OwnerPhone = doc.Vehicle?.Owner?.PhoneNumber ?? "N/A"
                };


                return new ResponseDTO("Success", 200, true, detail);
            }
            catch (Exception ex)
            {
                return new ResponseDTO(ex.Message, 500, false);
            }
        }
    }
}