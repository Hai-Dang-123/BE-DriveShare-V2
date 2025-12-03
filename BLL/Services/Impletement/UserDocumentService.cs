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
        // 2. CREATE AND VERIFY (HỖ TRỢ CCCD VÀ GPLX)
        // ============================================================
        public async Task<ResponseDTO> CreateAndVerifyDocumentAsync(IFormFile frontImg, IFormFile? backImg, IFormFile? selfieImg, DocumentType docType)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Người dùng không hợp lệ.", 401, false);

                // Validation đầu vào
                if (frontImg == null) return new ResponseDTO("Bắt buộc phải có ảnh mặt trước.", 400, false);

                if (docType == DocumentType.CCCD)
                {
                    if (backImg == null || selfieImg == null)
                        return new ResponseDTO("CCCD yêu cầu đủ 3 ảnh (Trước, Sau, Chân dung).", 400, false);
                }

                // Check đã tồn tại ACTIVE chưa (Tránh spam)
                bool exists = await _unitOfWork.UserDocumentRepo.GetAll()
                    .AnyAsync(x => x.UserId == userId && x.DocumentType == docType && x.Status == VerifileStatus.ACTIVE);

                if (exists) return new ResponseDTO($"Bạn đã có hồ sơ {docType} được xác thực rồi.", 400, false);

                // --- 1. Clone Files (Để dùng nhiều lần) ---
                var frontEkyc = await CloneFileToMemoryAsync(frontImg);
                var backEkyc = backImg != null ? await CloneFileToMemoryAsync(backImg) : null;
                var selfieEkyc = selfieImg != null ? await CloneFileToMemoryAsync(selfieImg) : null;

                // --- 2. Upload Firebase (Chạy Song song) ---
                var uploadTasks = new List<Task<string?>>();

                var frontUrlTask = _firebaseUploadService.UploadFileAsync(await CloneFileToMemoryAsync(frontImg), userId, FirebaseFileType.USER_DOCUMENTS);
                uploadTasks.Add(frontUrlTask);

                Task<string?>? backUrlTask = null;
                if (backImg != null)
                {
                    backUrlTask = _firebaseUploadService.UploadFileAsync(await CloneFileToMemoryAsync(backImg), userId, FirebaseFileType.USER_DOCUMENTS);
                    uploadTasks.Add(backUrlTask);
                }

                Task<string?>? selfieUrlTask = null;
                if (selfieImg != null)
                {
                    selfieUrlTask = _firebaseUploadService.UploadFileAsync(await CloneFileToMemoryAsync(selfieImg), userId, FirebaseFileType.USER_DOCUMENTS);
                    uploadTasks.Add(selfieUrlTask);
                }

                // --- 3. Gọi EKYC Verify (VNPT) ---
                // Truyền docType vào để Service biết gọi API nào (OCR Full hay OCR Front Only)
                var ekycTask = _ekycService.VerifyIdentityAsync(frontEkyc, backEkyc, selfieEkyc, docType);

                // Chờ tất cả hoàn thành
                await Task.WhenAll(uploadTasks);
                var ekycResult = await ekycTask;

                // --- 4. Xử lý kết quả EKYC ---
                if (!ekycResult.IsSuccess || ekycResult.OcrData == null)
                {
                    return new ResponseDTO($"Lỗi EKYC: {ekycResult.ErrorMessage}", 400, false);
                }

                // --- 5. Map vào Entity ---
                var userDoc = new UserDocument
                {
                    UserDocumentId = Guid.NewGuid(),
                    UserId = userId,
                    DocumentType = docType,

                    // URLs từ Firebase
                    FrontImageUrl = await frontUrlTask,
                    BackImageUrl = backUrlTask != null ? await backUrlTask : null,
                    PortraitImageUrl = selfieUrlTask != null ? await selfieUrlTask : null,

                    // Hashes từ VNPT
                    FrontImageHash = ekycResult.FrontHash,
                    BackImageHash = ekycResult.BackHash,
                    PortraitImageHash = ekycResult.FaceHash,

                    // Thông tin OCR
                    IdentityNumber = ekycResult.OcrData.Id,
                    FullName = ekycResult.OcrData.Name,
                    DateOfBirth = ParseVnptDate(ekycResult.OcrData.BirthDay),
                    IssueDate = ParseVnptDate(ekycResult.OcrData.IssueDate),
                    ExpiryDate = ParseVnptDate(ekycResult.OcrData.ValidDate),

                    PlaceOfOrigin = ekycResult.OcrData.OriginLocation,
                    PlaceOfResidence = ekycResult.OcrData.RecentLocation,
                    IssuePlace = ekycResult.OcrData.IssuePlace,

                    // Kết quả phân tích
                    IsDocumentReal = ekycResult.IsRealCard,
                    FaceMatchScore = ekycResult.FaceMatchScore,

                    // LƯU LOG RAW JSON
                    EkycLog = ekycResult.OcrRawJson,

                    CreatedAt = DateTime.UtcNow,
                    Status = VerifileStatus.INACTIVE // Mặc định chưa duyệt
                };

                // Nếu là GPLX thì lấy hạng bằng (Rank)
                if (docType == DocumentType.DRIVER_LINCENSE)
                {
                    userDoc.LicenseClass = ekycResult.OcrData.Rank;
                }

                // --- 6. Logic Duyệt Tự Động ---
                var rejectReasons = new List<string>();

                // Check 1: Giấy tờ thật
                if (!ekycResult.IsRealCard)
                    rejectReasons.Add("Giấy tờ có dấu hiệu giả mạo hoặc chụp qua màn hình.");

                // Check 2: Khớp khuôn mặt (Chỉ check nếu có ảnh selfie)
                if (selfieImg != null && (ekycResult.FaceMatchScore ?? 0) < 85)
                    rejectReasons.Add($"Khuôn mặt không khớp (Độ khớp: {ekycResult.FaceMatchScore:F2}% < 85%).");

                // Check 3: Tampering (Can thiệp chỉnh sửa)
                if (ekycResult.OcrData.Tampering != null && ekycResult.OcrData.Tampering.IsLegal == "no")
                {
                    rejectReasons.Add("Giấy tờ có dấu hiệu bị chỉnh sửa/cắt ghép.");
                }

                // Check 4: Warning Messages từ VNPT (Mờ, nhòe, mất góc...)
                if (ekycResult.OcrData.WarningMsg != null && ekycResult.OcrData.WarningMsg.Any())
                {
                    rejectReasons.AddRange(ekycResult.OcrData.WarningMsg);
                }

                // Quyết định Status
                if (rejectReasons.Count == 0)
                {
                    userDoc.Status = VerifileStatus.ACTIVE;
                    userDoc.VerifiedAt = DateTime.UtcNow;

                    // Cập nhật thông tin User nếu cần (ví dụ update tên thật từ giấy tờ)
                    var user = await _unitOfWork.BaseUserRepo.GetByIdAsync(userId);
                    if (user != null)
                    {
                        user.LastUpdatedAt = DateTime.UtcNow;
                        await _unitOfWork.BaseUserRepo.UpdateAsync(user);
                    }
                }
                else
                {
                    userDoc.Status = VerifileStatus.INACTIVE; // Hoặc REJECTED
                    userDoc.RejectionReason = string.Join("; ", rejectReasons);
                }

                // Lưu vào DB
                await _unitOfWork.UserDocumentRepo.AddAsync(userDoc);
                await _unitOfWork.SaveChangeAsync();

                // Trả về kết quả
                if (userDoc.Status == VerifileStatus.ACTIVE)
                    return new ResponseDTO($"Xác thực {docType} thành công.", 200, true, MapToDetailDTO(userDoc));
                else
                    return new ResponseDTO($"Xác thực thất bại: {userDoc.RejectionReason}", 400, false, new { Reason = userDoc.RejectionReason });

            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi hệ thống: {ex.Message}", 500, false);
            }
        }

        // ============================================================
        // CÁC HÀM GET (GIỮ NGUYÊN)
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

                var allDocs = await _unitOfWork.UserDocumentRepo.GetAll()
                    .Where(x => x.UserId == userId) // Lấy hết để hiện cả cái bị từ chối
                    .AsNoTracking()
                    .ToListAsync();

                // Lấy CCCD mới nhất
                var cccdEntity = allDocs
                    .Where(x => x.DocumentType == DocumentType.CCCD)
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefault();

                var response = new MyDocumentsResponseDTO
                {
                    IsDriver = false,
                    CCCD = MapToDetailDTO(cccdEntity)
                };

                if (user.Role != null && user.Role.RoleName.ToUpper().Contains("DRIVER"))
                {
                    response.IsDriver = true;
                    // Lấy GPLX mới nhất
                    var licenseEntity = allDocs
                        .Where(x => x.DocumentType == DocumentType.DRIVER_LINCENSE)
                        .OrderByDescending(x => x.CreatedAt)
                        .FirstOrDefault();

                    response.DriverDocuments = new DriverDocumentsDTO
                    {
                        DrivingLicense = MapToDetailDTO(licenseEntity)
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
    }
}