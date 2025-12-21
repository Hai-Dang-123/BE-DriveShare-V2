using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
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
                    CreatedAt = TimeUtil.NowVN(),
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

                // 2. Validate & Upload Firebase
                if (dto.FrontFile == null || dto.FrontFile.Length == 0) return new ResponseDTO("Thiếu ảnh mặt trước.", 400, false);

                var frontUrl = await _firebaseService.UploadFileAsync(dto.FrontFile, userId, FirebaseFileType.VEHICLE_DOCUMENTS);
                string? backUrl = null;
                if (dto.BackFile != null)
                {
                    backUrl = await _firebaseService.UploadFileAsync(dto.BackFile, userId, FirebaseFileType.VEHICLE_DOCUMENTS);
                }

                // 3. Tạo Document Entity
                var document = new VehicleDocument
                {
                    VehicleDocumentId = Guid.NewGuid(),
                    VehicleId = vehicleId,
                    DocumentType = dto.DocumentType,
                    ExpirationDate = dto.ExpirationDate,
                    FrontDocumentUrl = frontUrl,
                    BackDocumentUrl = backUrl,
                    Status = VerifileStatus.PENDING_REVIEW,
                    CreatedAt = TimeUtil.NowVN()
                };

                // [NEW] Nếu là giấy Đăng kiểm, lưu tạm thông tin vào Note hoặc trường phụ để Staff duyệt sau
                // Ở đây mình giả sử lưu vào AdminNotes tạm thời để Staff thấy khi review
                if (dto.DocumentType == DocumentType.INSPECTION_CERTIFICATE && dto.InspectionDate.HasValue)
                {
                    // Format chuỗi JSON hoặc text để lưu tạm
                    document.AdminNotes = $"[USER_SUBMIT] Date: {dto.InspectionDate:dd/MM/yyyy} | Station: {dto.InspectionStation}";
                }

                await _unitOfWork.VehicleDocumentRepo.AddAsync(document);
                await _unitOfWork.SaveChangeAsync();

                // Response DTO (Giữ nguyên)
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
        // 3. STAFF DUYỆT GIẤY TỜ & TẠO LỊCH SỬ ĐĂNG KIỂM TỰ ĐỘNG
        // =========================================================================
        public async Task<ResponseDTO> ReviewVehicleDocumentAsync(Guid documentId, bool isApproved, string? rejectReason)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var doc = await _unitOfWork.VehicleDocumentRepo.GetByIdAsync(documentId);
                if (doc == null) return new ResponseDTO("Giấy tờ không tồn tại.", 404, false);

                if (isApproved)
                {
                    // A. Cập nhật trạng thái Document
                    doc.Status = VerifileStatus.ACTIVE;
                    doc.ProcessedAt = TimeUtil.NowVN();
                    doc.AdminNotes = "Approved by Staff.";

                    // B. Cập nhật trạng thái XE -> ACTIVE
                    var vehicle = await _unitOfWork.VehicleRepo.GetByIdAsync(doc.VehicleId);
                    if (vehicle != null)
                    {
                        vehicle.Status = VehicleStatus.ACTIVE;
                        await _unitOfWork.VehicleRepo.UpdateAsync(vehicle);
                    }

                    // C. [NEW] TẠO LỊCH SỬ ĐĂNG KIỂM (Nếu loại giấy là INSPECTION_CERTIFICATE)
                    if (doc.DocumentType == DocumentType.INSPECTION_CERTIFICATE && doc.ExpirationDate.HasValue)
                    {
                        // Parse thông tin từ AdminNotes (nếu có lưu ở bước Add) hoặc lấy mặc định
                        DateTime inspectionDate = TimeUtil.NowVN(); // Mặc định hôm nay nếu ko có data
                        string station = "N/A";

                        if (!string.IsNullOrEmpty(doc.AdminNotes) && doc.AdminNotes.Contains("[USER_SUBMIT]"))
                        {
                            // Logic parse đơn giản (Tùy bạn implement kỹ hơn)
                            // VD: "[USER_SUBMIT] Date: 12/12/2025 | Station: 50-05V"
                            // ... Code parse string ...
                        }

                        var history = new InspectionHistory
                        {
                            InspectionHistoryId = Guid.NewGuid(),
                            VehicleId = doc.VehicleId,
                            VehicleDocumentId = doc.VehicleDocumentId,
                            InspectionDate = inspectionDate,
                            ExpirationDate = doc.ExpirationDate.Value, // Lấy từ giấy tờ
                            InspectionStation = station,
                            Result = "ĐẠT", // Mặc định Đạt nếu được duyệt
                            CreatedAt = TimeUtil.NowVN()
                        };

                        await _unitOfWork.InspectionHistoryRepo.AddAsync(history); // Cần thêm Repo này
                    }
                }
                else
                {
                    // Từ chối
                    if (string.IsNullOrWhiteSpace(rejectReason)) return new ResponseDTO("Vui lòng nhập lý do từ chối.", 400, false);
                    doc.Status = VerifileStatus.REJECTED;
                    doc.ProcessedAt = TimeUtil.NowVN();
                    doc.AdminNotes = rejectReason;

                    // Nếu từ chối, có thể set xe về INACTIVE tùy logic
                }

                await _unitOfWork.VehicleDocumentRepo.UpdateAsync(doc);
                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                return new ResponseDTO(isApproved ? "Đã duyệt và cập nhật hồ sơ xe." : "Đã từ chối giấy tờ.", 200, true);
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

        public async Task<ResponseDTO> GetVehicleDocumentsByVehicleIdAsync(Guid vehicleId)
        {
            try
            {
                var documents = await _unitOfWork.VehicleDocumentRepo.GetAll()
                    .AsNoTracking()
                    .Where(d => d.VehicleId == vehicleId)
                    .OrderByDescending(d => d.CreatedAt)
                    .Select(d => new VehicleDocumentByVehicleDTO
                    {
                        VehicleDocumentId = d.VehicleDocumentId,

                        DocumentType = (int)d.DocumentType,
                        DocumentTypeName = d.DocumentType.ToString(),

                        Status = (int)d.Status,
                        StatusName = d.Status.ToString(),

                        FrontDocumentUrl = d.FrontDocumentUrl,
                        BackDocumentUrl = d.BackDocumentUrl,

                        ExpirationDate = d.ExpirationDate,
                        AdminNotes = d.AdminNotes,

                        CreatedAt = d.CreatedAt,
                        ProcessedAt = d.ProcessedAt
                    })
                    .ToListAsync();

                return new ResponseDTO("Success", 200, true, documents);
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

                // [NEW] Lấy danh sách lịch sử đăng kiểm của xe này để Staff tham khảo
                // (Giúp Staff biết xe này đã đăng kiểm bao nhiêu lần rồi)
                var historyList = await _unitOfWork.InspectionHistoryRepo.GetAll()
                    .Where(h => h.VehicleId == doc.VehicleId)
                    .OrderByDescending(h => h.InspectionDate)
                    .Take(5) // Lấy 5 lần gần nhất
                    .Select(h => new InspectionHistoryDTO
                    {
                        Date = h.InspectionDate,
                        ExpDate = h.ExpirationDate,
                        Station = h.InspectionStation,
                        Result = h.Result
                    })
                    .ToListAsync();

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
                    OwnerPhone = doc.Vehicle?.Owner?.PhoneNumber ?? "N/A",

                    // [NEW] Trường mới trả về cho FE
                    InspectionHistories = historyList
                };


                return new ResponseDTO("Success", 200, true, detail);
            }
            catch (Exception ex)
            {
                return new ResponseDTO(ex.Message, 500, false);
            }
        }

        public async Task<ResponseDTO> GetHistoryByVehicleIdAsync(Guid vehicleId)
        {
            try
            {
                var history = await _unitOfWork.InspectionHistoryRepo.GetAll()
                    .Where(x => x.VehicleId == vehicleId)
                    .OrderByDescending(x => x.InspectionDate) // Mới nhất lên đầu
                    .Select(h => new InspectionHistoryDTO
                    {
                        Date = h.InspectionDate,
                        ExpDate = h.ExpirationDate,
                        Station = h.InspectionStation,
                        Result = h.Result
                    })
                    .ToListAsync();

                return new ResponseDTO("Success", 200, true, history);
            }
            catch (Exception ex)
            {
                return new ResponseDTO(ex.Message, 500, false);
            }
        }
    }
}