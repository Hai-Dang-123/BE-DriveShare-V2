using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
using Common.Settings;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class UserDocumentService : IUserDocumentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;
        private readonly IFirebaseUploadService _firebaseUploadService;
        private readonly IEKYCService _ekycService;

        public UserDocumentService(IUnitOfWork unitOfWork, UserUtility userUtility, IFirebaseUploadService firebaseUploadService, IEKYCService ekycService)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _firebaseUploadService = firebaseUploadService;
            _ekycService = ekycService;
        }

        // ============================================================
        // 1. CHECK VERIFIED (LOGIC MỚI)
        // ============================================================
        public async Task<ResponseDTO> CheckCCCDVerifiedAsync()
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                // Lấy thông tin User, Role và các giấy tờ ACTIVE
                var user = await _unitOfWork.BaseUserRepo.GetAll()
                    .Include(u => u.Role)
                    .Include(u => u.UserDocuments)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user == null) return new ResponseDTO("User not found", 404, false);

                // Lọc các giấy tờ đã Active
                var activeDocs = user.UserDocuments.Where(d => d.Status == VerifileStatus.ACTIVE).ToList();
                bool hasCCCD = activeDocs.Any(d => d.DocumentType == DocumentType.CCCD);

                // Nếu là TÀI XẾ (DRIVER)
                if (user.Role != null && user.Role.RoleName.ToUpper().Contains("DRIVER"))
                {
                    bool hasLicense = activeDocs.Any(d => d.DocumentType == DocumentType.DRIVER_LINCENSE);

                    if (hasCCCD && hasLicense)
                    {
                        return new ResponseDTO("Tài xế đã xác thực đầy đủ (CCCD & GPLX).", 200, true, true);
                    }
                    else
                    {
                        var missing = new List<string>();
                        if (!hasCCCD) missing.Add("CCCD");
                        if (!hasLicense) missing.Add("GPLX");
                        return new ResponseDTO($"Tài xế chưa xác thực đủ giấy tờ. Thiếu: {string.Join(", ", missing)}", 200, true, false);
                    }
                }

                // Nếu là OWNER hoặc USER thường
                else
                {
                    if (hasCCCD)
                    {
                        return new ResponseDTO("Người dùng đã xác thực CCCD.", 200, true, true);
                    }
                    else
                    {
                        return new ResponseDTO("Người dùng chưa xác thực CCCD.", 200, true, false);
                    }
                }
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi hệ thống: {ex.Message}", 500, false);
            }
        }

        // ============================================================
        // 2. CREATE AND VERIFY (TRANSACTION + AUTO LOGIC)
        // ============================================================
        public async Task<ResponseDTO> CreateAndVerifyDocumentAsync(IFormFile frontImg, IFormFile? backImg, IFormFile? selfieImg, DocumentType docType)
        {
            // Sử dụng Transaction để đảm bảo tính toàn vẹn dữ liệu
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                // Validation
                if (frontImg == null) return new ResponseDTO("Thiếu ảnh mặt trước.", 400, false);
                if (docType == DocumentType.CCCD && (backImg == null || selfieImg == null))
                    return new ResponseDTO("CCCD cần 3 ảnh.", 400, false);

                // Check Active Exists (Tối ưu query với AnyAsync)
                bool exists = await _unitOfWork.UserDocumentRepo.GetAll()
                    .AnyAsync(x => x.UserId == userId && x.DocumentType == docType && x.Status == VerifileStatus.ACTIVE);
                if (exists) return new ResponseDTO($"Đã có hồ sơ {docType} hợp lệ.", 400, false);

                // --- A. XỬ LÝ UPLOAD ẢNH (Common) ---
                var uploadTasks = new List<Task<string?>>();
                var frontTask = _firebaseUploadService.UploadFileAsync(await CloneFileToMemoryAsync(frontImg), userId, FirebaseFileType.USER_DOCUMENTS);
                uploadTasks.Add(frontTask);

                Task<string?>? backTask = backImg != null ? _firebaseUploadService.UploadFileAsync(await CloneFileToMemoryAsync(backImg), userId, FirebaseFileType.USER_DOCUMENTS) : null;
                if (backTask != null) uploadTasks.Add(backTask);

                Task<string?>? selfieTask = selfieImg != null ? _firebaseUploadService.UploadFileAsync(await CloneFileToMemoryAsync(selfieImg), userId, FirebaseFileType.USER_DOCUMENTS) : null;
                if (selfieTask != null) uploadTasks.Add(selfieTask);

                // --- B. PHÂN NHÁNH XỬ LÝ THEO LOẠI ---

                // CASE 1: GIẤY KHÁM SỨC KHỎE -> Manual Review (Không gọi VNPT)
                if (docType == DocumentType.HEALTH_CHECK)
                {
                    await Task.WhenAll(uploadTasks);

                    var healthDoc = new UserDocument
                    {
                        UserDocumentId = Guid.NewGuid(),
                        UserId = userId,
                        DocumentType = docType,
                        FrontImageUrl = await frontTask,
                        CreatedAt = DateTime.UtcNow,
                        Status = VerifileStatus.PENDING_REVIEW, // Chờ staff duyệt
                        RejectionReason = "Đang chờ nhân viên duyệt."
                    };

                    await _unitOfWork.UserDocumentRepo.AddAsync(healthDoc);
                    await _unitOfWork.SaveChangeAsync();
                    await transaction.CommitAsync(); // Commit ngay

                    // Có thể bắn Notification cho Admin ở đây (Task.Run...)

                    return new ResponseDTO("Đã gửi Giấy khám sức khỏe, vui lòng chờ duyệt.", 200, true, MapToDetailDTO(healthDoc));
                }

                // CASE 2: CCCD / GPLX -> Auto Verify (VNPT)
                var frontEkyc = await CloneFileToMemoryAsync(frontImg);
                var backEkyc = backImg != null ? await CloneFileToMemoryAsync(backImg) : null;
                var selfieEkyc = selfieImg != null ? await CloneFileToMemoryAsync(selfieImg) : null;

                var ekycResult = await _ekycService.VerifyIdentityAsync(frontEkyc, backEkyc, selfieEkyc, docType);
                await Task.WhenAll(uploadTasks); // Đợi upload xong

                if (!ekycResult.IsSuccess || ekycResult.OcrData == null)
                    throw new Exception($"Lỗi EKYC: {ekycResult.ErrorMessage}");

                var userDoc = new UserDocument
                {
                    UserDocumentId = Guid.NewGuid(),
                    UserId = userId,
                    DocumentType = docType,
                    // URLs
                    FrontImageUrl = await frontTask,
                    BackImageUrl = backTask != null ? await backTask : null,
                    PortraitImageUrl = selfieTask != null ? await selfieTask : null,
                    // OCR Data
                    IdentityNumber = ekycResult.OcrData.Id,
                    FullName = ekycResult.OcrData.Name,
                    DateOfBirth = ParseVnptDate(ekycResult.OcrData.BirthDay),
                    ExpiryDate = ParseVnptDate(ekycResult.OcrData.ValidDate),
                    IssueDate = ParseVnptDate(ekycResult.OcrData.IssueDate),
                    PlaceOfOrigin = ekycResult.OcrData.OriginLocation,
                    PlaceOfResidence = ekycResult.OcrData.RecentLocation,
                    // Analysis
                    IsDocumentReal = ekycResult.IsRealCard,
                    FaceMatchScore = ekycResult.FaceMatchScore,
                    EkycLog = ekycResult.OcrRawJson,

                    CreatedAt = DateTime.UtcNow,
                    Status = VerifileStatus.INACTIVE
                };

                if (docType == DocumentType.DRIVER_LINCENSE) userDoc.LicenseClass = ekycResult.OcrData.Rank;

                // --- Auto Approve Logic ---
                var rejectReasons = new List<string>();
                if (!ekycResult.IsRealCard) rejectReasons.Add("Giấy tờ giả mạo/chụp màn hình.");
                if (selfieImg != null && (ekycResult.FaceMatchScore ?? 0) < 85) rejectReasons.Add("Khuôn mặt không khớp.");
                if (ekycResult.OcrData.Tampering?.IsLegal == "no") rejectReasons.Add("Giấy tờ bị chỉnh sửa.");
                if (ekycResult.OcrData.WarningMsg?.Any() == true) rejectReasons.AddRange(ekycResult.OcrData.WarningMsg);

                if (rejectReasons.Count == 0)
                {
                    userDoc.Status = VerifileStatus.ACTIVE;
                    userDoc.VerifiedAt = DateTime.UtcNow;

                    // Update Base Info
                    if (docType == DocumentType.DRIVER_LINCENSE)
                    {
                        var driver = await _unitOfWork.DriverRepo.GetByIdAsync(userId);
                        if (driver != null)
                        {
                            driver.LicenseNumber = userDoc.IdentityNumber;
                            driver.LicenseClass = userDoc.LicenseClass;
                            driver.LicenseExpiryDate = userDoc.ExpiryDate;
                            driver.IsLicenseVerified = true;
                            await _unitOfWork.DriverRepo.UpdateAsync(driver);
                        }
                    }
                    else if (docType == DocumentType.CCCD)
                    {
                        var user = await _unitOfWork.BaseUserRepo.GetByIdAsync(userId);
                        if (user != null)
                        {
                            user.FullName = userDoc.FullName;
                            if (userDoc.DateOfBirth.HasValue) user.DateOfBirth = userDoc.DateOfBirth.Value;
                            await _unitOfWork.BaseUserRepo.UpdateAsync(user);
                        }
                    }
                }
                else
                {
                    userDoc.Status = VerifileStatus.INACTIVE;
                    userDoc.RejectionReason = string.Join("; ", rejectReasons);
                }

                await _unitOfWork.UserDocumentRepo.AddAsync(userDoc);
                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                return userDoc.Status == VerifileStatus.ACTIVE
                    ? new ResponseDTO("Xác thực thành công.", 200, true, MapToDetailDTO(userDoc))
                    : new ResponseDTO("Xác thực thất bại.", 400, false, new { Reason = userDoc.RejectionReason });

            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO($"Lỗi: {ex.Message}", 500, false);
            }
        }

        // ============================================================
        // CÁC HÀM GET (GIỮ NGUYÊN)
        // ============================================================
        // ============================================================
        // CÁC HÀM GET (ĐÃ BỔ SUNG GKSK)
        // ============================================================
        public async Task<ResponseDTO> GetMyVerifiedDocumentsAsync()
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                var user = await _unitOfWork.BaseUserRepo.GetAll()
                    .Include(u => u.Role)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user == null) return new ResponseDTO("User not found", 404, false);

                // Lấy tất cả document của user (bao gồm cả active, rejected, pending...)
                var allDocs = await _unitOfWork.UserDocumentRepo.GetAll()
                    .Where(x => x.UserId == userId)
                    .AsNoTracking()
                    .ToListAsync();

                // 1. Lấy CCCD mới nhất
                var cccdEntity = allDocs
                    .Where(x => x.DocumentType == DocumentType.CCCD)
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefault();

                var response = new MyDocumentsResponseDTO
                {
                    IsDriver = false,
                    CCCD = MapToDetailDTO(cccdEntity)
                };

                // 2. Nếu là Tài xế -> Lấy thêm GPLX và GKSK
                if (user.Role != null && user.Role.RoleName.ToUpper().Contains("DRIVER"))
                {
                    response.IsDriver = true;

                    // Lấy Bằng lái xe mới nhất
                    var licenseEntity = allDocs
                        .Where(x => x.DocumentType == DocumentType.DRIVER_LINCENSE)
                        .OrderByDescending(x => x.CreatedAt)
                        .FirstOrDefault();

                    // [MỚI] Lấy Giấy khám sức khỏe mới nhất
                    var healthEntity = allDocs
                        .Where(x => x.DocumentType == DocumentType.HEALTH_CHECK)
                        .OrderByDescending(x => x.CreatedAt)
                        .FirstOrDefault();

                    response.DriverDocuments = new DriverDocumentsDTO
                    {
                        DrivingLicense = MapToDetailDTO(licenseEntity),
                        HealthCheck = MapToDetailDTO(healthEntity) // <--- Bổ sung dòng này
                    };
                }

                return new ResponseDTO("Lấy thông tin giấy tờ thành công.", 200, true, response);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi hệ thống: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> GetAllAsync()
        {
            try
            {
                var docs = await _unitOfWork.UserDocumentRepo.GetAll()
                    .AsNoTracking()
                    .ToListAsync();
                return new ResponseDTO("Success", 200, true, docs);
            }
            catch (Exception ex) { return new ResponseDTO(ex.Message, 500, false); }
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            try
            {
                var doc = await _unitOfWork.UserDocumentRepo.GetAll().AsNoTracking().FirstOrDefaultAsync(x => x.UserDocumentId == id);
                if (doc == null) return new ResponseDTO("Not found", 404, false);
                return new ResponseDTO("Success", 200, true, doc);
            }
            catch (Exception ex) { return new ResponseDTO(ex.Message, 500, false); }
        }

        public async Task<ResponseDTO> GetByUserIdAsync(Guid userId)
        {
            try
            {
                var user = await _unitOfWork.BaseUserRepo.GetAll()
                    .Include(u => u.Role).Include(u => u.UserDocuments)
                    .AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);

                if (user == null) return new ResponseDTO("User not found", 404, false);

                var userDto = new UserWithDocumentsDTO
                {
                    UserId = user.UserId,
                    FullName = user.FullName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    AvatarUrl = user.AvatarUrl,
                    RoleName = user.Role?.RoleName ?? "",
                    Status = user.Status.ToString(),
                    CreatedAt = user.CreatedAt,
                    Documents = user.UserDocuments.Select(d => new UserDocumentDTO
                    {
                        UserDocumentId = d.UserDocumentId,
                        UserId = d.UserId,
                        DocumentType = d.DocumentType.ToString(),
                        FrontImageUrl = d.FrontImageUrl,
                        BackImageUrl = d.BackImageUrl,
                        Status = d.Status.ToString(),
                        RejectionReason = d.RejectionReason,
                        CreatedAt = d.CreatedAt,
                        VerifiedAt = d.VerifiedAt
                    }).ToList()
                };
                return new ResponseDTO("Success", 200, true, userDto);
            }
            catch (Exception ex) { return new ResponseDTO(ex.Message, 500, false); }
        }

        // ================== HELPER METHODS ==================

        private async Task<IFormFile> CloneFileToMemoryAsync(IFormFile file)
        {
            if (file == null) return null;
            var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;
            var contentType = !string.IsNullOrEmpty(file.ContentType) ? file.ContentType : "image/jpeg";
            var fileName = !string.IsNullOrEmpty(file.FileName) ? file.FileName : "unknown.jpg";
            return new CustomFormFile(stream, file.Name, fileName, contentType);
        }

        private DateTime? ParseVnptDate(string? dateStr)
        {
            if (string.IsNullOrEmpty(dateStr) || dateStr == "-" || dateStr == "N/A" || dateStr == "Không thời hạn") return null;
            if (DateTime.TryParseExact(dateStr, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return date;
            return null;
        }

        private DocumentDetailDTO? MapToDetailDTO(UserDocument? doc)
        {
            if (doc == null) return null;
            return new DocumentDetailDTO
            {
                UserDocumentId = doc.UserDocumentId,
                DocumentType = doc.DocumentType.ToString(),
                Status = doc.Status.ToString(),
                IdentityNumber = doc.IdentityNumber ?? "",
                FullName = doc.FullName ?? "",
                LicenseClass = doc.LicenseClass,
                FrontImageUrl = doc.FrontImageUrl,
                BackImageUrl = doc.BackImageUrl ?? "",
                ExpiryDate = doc.ExpiryDate,
                VerifiedAt = doc.VerifiedAt,
                RejectionReason = doc.RejectionReason
            };
        }

        public Task<ResponseDTO> UpdateAsync(Guid id, UserDocumentDTO dto) => Task.FromResult(new ResponseDTO("Not supported", 400, false));
        public Task<ResponseDTO> DeleteAsync(Guid id) => Task.FromResult(new ResponseDTO("Not supported", 400, false));

        public async Task<(bool IsValid, string Message)> ValidateUserDocumentsAsync(Guid userId)
        {
            // 1. Lấy thông tin User (Chỉ lấy Role và List Document active để tối ưu query)
            var user = await _unitOfWork.BaseUserRepo.GetAll()
                .AsNoTracking()
                .Include(u => u.Role)
                .Include(u => u.UserDocuments)
                .Where(u => u.UserId == userId)
                .Select(u => new
                {
                    RoleName = u.Role != null ? u.Role.RoleName : "",
                    ActiveDocTypes = u.UserDocuments
                                      .Where(d => d.Status == VerifileStatus.ACTIVE)
                                      .Select(d => d.DocumentType)
                                      .ToList()
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return (false, "Không tìm thấy thông tin người dùng.");

            // 2. Check xem có CCCD chưa (Ai cũng cần)
            bool hasCCCD = user.ActiveDocTypes.Contains(DocumentType.CCCD);

            // 3. Logic check theo Role
            // Case: TÀI XẾ (DRIVER)
            if (user.RoleName.ToUpper().Contains("DRIVER"))
            {
                bool hasLicense = user.ActiveDocTypes.Contains(DocumentType.DRIVER_LINCENSE);

                if (hasCCCD && hasLicense)
                {
                    return (true, "Đã xác thực đầy đủ.");
                }

                // Báo lỗi chi tiết thiếu cái gì
                var missing = new List<string>();
                if (!hasCCCD) missing.Add("CCCD/CMND");
                if (!hasLicense) missing.Add("Bằng lái xe");

                return (false, $"Tài xế cần xác thực: {string.Join(", ", missing)}.");
            }

            // Case: CÁC ROLE KHÁC (PROVIDER, OWNER...)
            // Chỉ cần CCCD là đủ đăng bài
            if (!hasCCCD)
            {
                return (false, "Bạn cần xác thực CCCD (eKYC) và được duyệt trước khi thực hiện chức năng này.");
            }

            // Nếu OK hết
            return (true, "Hợp lệ.");
        }

        // ============================================================
        // 4. REQUEST MANUAL REVIEW (USER)
        // ============================================================
        public async Task<ResponseDTO> RequestManualReviewAsync(RequestManualReviewDTO dto)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                var doc = await _unitOfWork.UserDocumentRepo.GetByIdAsync(dto.UserDocumentId);

                if (doc == null) return new ResponseDTO("Not found", 404, false);
                if (doc.UserId != userId) return new ResponseDTO("Forbidden", 403, false);
                if (doc.Status == VerifileStatus.ACTIVE) return new ResponseDTO("Đã Active rồi.", 400, false);

                doc.Status = VerifileStatus.PENDING_REVIEW;
                doc.RejectionReason = $"[USER REQUEST]: {dto.UserNote}";
                doc.LastUpdatedAt = DateTime.UtcNow;

                await _unitOfWork.UserDocumentRepo.UpdateAsync(doc);
                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                return new ResponseDTO("Đã gửi yêu cầu xem xét.", 200, true, MapToDetailDTO(doc));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO(ex.Message, 500, false);
            }
        }

        // ============================================================
        // 3. REVIEW DOCUMENT (TRANSACTION - ADMIN/STAFF)
        // ============================================================
        public async Task<ResponseDTO> ReviewDocumentAsync(ReviewDocumentDTO dto)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var doc = await _unitOfWork.UserDocumentRepo.GetByIdAsync(dto.UserDocumentId);
                if (doc == null) return new ResponseDTO("Tài liệu không tồn tại.", 404, false);

                if (doc.Status != VerifileStatus.PENDING_REVIEW && doc.Status != VerifileStatus.INACTIVE)
                    return new ResponseDTO("Trạng thái không hợp lệ để duyệt.", 400, false);

                if (dto.IsApproved)
                {
                    // Approve Logic
                    doc.Status = VerifileStatus.ACTIVE;
                    doc.VerifiedAt = DateTime.UtcNow;
                    doc.RejectionReason = null;
                    doc.IsDocumentReal = true; // Override AI/Default

                    // Đồng bộ dữ liệu sang bảng Driver nếu là GPLX hoặc GKSK (active)
                    if (doc.DocumentType == DocumentType.DRIVER_LINCENSE)
                    {
                        var driver = await _unitOfWork.DriverRepo.GetByIdAsync(doc.UserId);
                        if (driver != null)
                        {
                            // Nếu OCR fail trước đó, giờ admin nhập tay hoặc trust OCR cũ
                            if (!string.IsNullOrEmpty(doc.IdentityNumber)) driver.LicenseNumber = doc.IdentityNumber;
                            if (!string.IsNullOrEmpty(doc.LicenseClass)) driver.LicenseClass = doc.LicenseClass;
                            driver.IsLicenseVerified = true;
                            await _unitOfWork.DriverRepo.UpdateAsync(driver);
                        }
                    }
                    // GKSK: Chỉ cần status active là đủ điều kiện, không cần map field nào cụ thể
                }
                else
                {
                    // Reject Logic
                    if (string.IsNullOrWhiteSpace(dto.RejectReason)) return new ResponseDTO("Cần lý do từ chối.", 400, false);
                    doc.Status = VerifileStatus.REJECTED;
                    doc.RejectionReason = dto.RejectReason;
                    doc.VerifiedAt = null;
                }

                await _unitOfWork.UserDocumentRepo.UpdateAsync(doc);
                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                // Send Notification Logic here if needed

                return new ResponseDTO(dto.IsApproved ? "Đã duyệt." : "Đã từ chối.", 200, true, MapToDetailDTO(doc));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO($"Lỗi: {ex.Message}", 500, false);
            }
        }

        // ============================================================
        // 3.1. GET PENDING LIST (SUMMARY - TỐI ƯU HIỆU NĂNG)
        // ============================================================
        public async Task<ResponseDTO> GetPendingReviewListAsync(
            int pageNumber,
            int pageSize,
            string? search = null,
            string? sortField = null,
            string? sortOrder = "DESC")
        {
            try
            {
                // 1. Base Query (Chỉ lấy PENDING_REVIEW)
                var query = _unitOfWork.UserDocumentRepo.GetAll()
                    .AsNoTracking()
                    .Include(d => d.User) // Include User để lấy tên/email
                    .Where(d => d.Status == VerifileStatus.PENDING_REVIEW);

                // 2. Search (Tìm theo Tên User, Email, hoặc Loại giấy tờ)
                if (!string.IsNullOrWhiteSpace(search))
                {
                    string keyword = search.Trim().ToLower();
                    query = query.Where(d =>
                        (d.User.FullName != null && d.User.FullName.ToLower().Contains(keyword)) ||
                        (d.User.Email != null && d.User.Email.ToLower().Contains(keyword)) ||
                        d.DocumentType.ToString().ToLower().Contains(keyword)
                    );
                }

                // 3. Sort (Sắp xếp động)
                bool isDesc = (sortOrder?.ToUpper() == "DESC");
                query = sortField?.ToLower() switch
                {
                    "username" => isDesc ? query.OrderByDescending(d => d.User.FullName) : query.OrderBy(d => d.User.FullName),
                    "email" => isDesc ? query.OrderByDescending(d => d.User.Email) : query.OrderBy(d => d.User.Email),
                    "type" => isDesc ? query.OrderByDescending(d => d.DocumentType) : query.OrderBy(d => d.DocumentType),
                    "date" => isDesc ? query.OrderByDescending(d => d.LastUpdatedAt ?? d.CreatedAt) : query.OrderBy(d => d.LastUpdatedAt ?? d.CreatedAt), // Ưu tiên ngày update mới nhất (lúc user gửi request)
                    _ => query.OrderByDescending(d => d.LastUpdatedAt ?? d.CreatedAt) // Mặc định: Mới nhất lên đầu
                };

                // 4. Paging & Projection (Chỉ lấy field cần thiết)
                var totalCount = await query.CountAsync();
                var items = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(d => new PendingReviewSummaryDTO
                    {
                        UserDocumentId = d.UserDocumentId,
                        UserId = d.UserId,
                        UserName = d.User.FullName ?? "N/A",
                        Email = d.User.Email,
                        DocumentType = d.DocumentType.ToString(),
                        UserNote = d.RejectionReason ?? "", // Lý do user gửi
                        CreatedAt = d.CreatedAt,
                        LastUpdatedAt = d.LastUpdatedAt // Thời điểm gửi request
                    })
                    .ToListAsync();

                var paginatedResult = new PaginatedDTO<PendingReviewSummaryDTO>(items, totalCount, pageNumber, pageSize);
                return new ResponseDTO("Lấy danh sách chờ duyệt thành công.", 200, true, paginatedResult);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi: {ex.Message}", 500, false);
            }
        }

        // ============================================================
        // 3.2. GET PENDING DETAIL (FULL INFO + EKYC ANALYSIS)
        // ============================================================
        public async Task<ResponseDTO> GetPendingReviewDetailAsync(Guid userDocumentId)
        {
            try
            {
                var doc = await _unitOfWork.UserDocumentRepo.GetAll()
                    .AsNoTracking()
                    .Include(d => d.User)
                    .FirstOrDefaultAsync(d => d.UserDocumentId == userDocumentId);

                if (doc == null) return new ResponseDTO("Tài liệu không tồn tại.", 404, false);

                // Kiểm tra quyền truy cập nếu cần (ví dụ chỉ staff mới xem được status này)
                // if (doc.Status != VerifileStatus.PENDING_REVIEW) ...

                // Map sang Detail DTO
                var detailDto = new PendingReviewDetailDTO
                {
                    // Thông tin chung (kế thừa từ Summary)
                    UserDocumentId = doc.UserDocumentId,
                    UserId = doc.UserId,
                    UserName = doc.User?.FullName ?? "N/A",
                    Email = doc.User?.Email ?? "N/A",
                    DocumentType = doc.DocumentType.ToString(),
                    UserNote = doc.RejectionReason ?? "",
                    CreatedAt = doc.CreatedAt,
                    LastUpdatedAt = doc.LastUpdatedAt,

                    Status = doc.Status.ToString(),

                    // Thông tin chi tiết (Ảnh + Log)
                    FrontImageUrl = doc.FrontImageUrl,
                    BackImageUrl = doc.BackImageUrl,
                    PortraitImageUrl = doc.PortraitImageUrl,
                    EkycLog = doc.EkycLog,
                    RejectionReason = doc.RejectionReason,

                    // 🌟 PHÂN TÍCH EKYC LOG (Chỉ chạy khi xem detail)
                    AnalysisResult = AnalyzeEkycLog(doc.EkycLog)


                };

                return new ResponseDTO("Lấy chi tiết thành công.", 200, true, detailDto);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi: {ex.Message}", 500, false);
            }
        }

        // ============================================================
        // 9. PRIVATE HELPERS (ANALYZE LOG & MAPPERS)
        // ============================================================

        private EkycAnalysisResultDTO AnalyzeEkycLog(string? jsonLog)
        {
            var result = new EkycAnalysisResultDTO();
            if (string.IsNullOrWhiteSpace(jsonLog)) { result.Warnings.Add("No log data."); return result; }

            try
            {
                var root = JObject.Parse(jsonLog);
                var data = root["object"];
                if (data == null) return result;

                // Basic Info
                result.OcrName = data["name"]?.ToString() ?? "";
                result.OcrId = data["id"]?.ToString() ?? "";
                result.OcrBirthDay = data["birth_day"]?.ToString() ?? "";
                result.DocumentType = data["card_type"]?.ToString() ?? "";

                // Tampering Check
                var tampering = data["tampering"];
                if (tampering?["is_legal"]?.ToString().ToLower() == "no")
                {
                    result.IsValidDocument = false;
                    result.HasTampering = true;
                    result.Warnings.Add("Phát hiện chỉnh sửa/giả mạo (Tampering).");

                    var tw = tampering["warning"]?.ToObject<List<string>>();
                    if (tw != null) foreach (var w in tw) result.Warnings.Add(TranslateEkycCode(w));
                }
                else result.IsValidDocument = true;

                // General Warning
                var warnings = data["general_warning"]?.ToObject<List<string>>();
                if (warnings != null) foreach (var w in warnings) result.Warnings.Add(TranslateEkycCode(w));

                // Check Fake (Recapture/Edited)
                CheckFake(data["checking_result_front"], "Mặt trước", result);
                CheckFake(data["checking_result_back"], "Mặt sau", result);

                // Quality
                CheckQuality(data["quality_front"], "Mặt trước", result);
                CheckQuality(data["quality_back"], "Mặt sau", result);

                // Match Front-Back
                var match = data["match_front_back"];
                if (match != null)
                {
                    foreach (JProperty prop in match)
                        if (prop.Value.ToString().ToLower() == "no")
                        {
                            result.DataMismatch = true;
                            result.Warnings.Add($"Không khớp: {TranslateEkycCode(prop.Name)}");
                        }
                }
            }
            catch { result.Warnings.Add("Error parsing EKYC log."); }
            return result;
        }

        private void CheckFake(JToken? token, string side, EkycAnalysisResultDTO res)
        {
            if (token == null) return;
            if (token["recaptured_result"]?.ToString() == "1") { res.IsScreenRecapture = true; res.Warnings.Add($"{side}: Chụp qua màn hình."); }
            if (token["corner_cut_result"]?.ToString() == "1") { res.IsCornerCut = true; res.Warnings.Add($"{side}: Mất góc."); }
            if (token["edited_result"]?.ToString() == "1") { res.HasTampering = true; res.Warnings.Add($"{side}: Chỉnh sửa (Edited)."); }
        }

        private void CheckQuality(JToken? q, string side, EkycAnalysisResultDTO res)
        {
            if (q?["final_result"] == null) return;
            var f = q["final_result"];
            if (f["blurred_likelihood"]?.ToString() != "unlikely") res.Warnings.Add($"{side}: Mờ.");
            if (f["bright_spot_likelihood"]?.ToString() != "unlikely") res.Warnings.Add($"{side}: Lóa sáng.");
        }

        private string TranslateEkycCode(string code)
        {
            // Simple translation map
            code = code.ToLower();
            if (code.Contains("mat_goc")) return "Mất góc";
            if (code.Contains("het_han")) return "Hết hạn";
            if (code.Contains("match_name")) return "Họ tên";
            if (code.Contains("match_id")) return "Số ID";
            if (code.Contains("match_bod")) return "Ngày sinh";
            if (code.Contains("match_valid_date")) return "Ngày hết hạn";
            return code;
        }
    }
}