using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.DTOs.TripVehicleHandoverRecord;
using Common.Enums.Status;
using Common.Enums.Type;
using Common.Settings;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class TripVehicleHandoverRecordService : ITripVehicleHandoverRecordService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;
        private readonly IEmailService _emailService;
        private readonly IFirebaseUploadService _firebaseUploadService;
        private readonly INotificationService _notificationService;

        public TripVehicleHandoverRecordService(IUnitOfWork unitOfWork, UserUtility userUtility, IEmailService emailService, IFirebaseUploadService firebaseUploadService, INotificationService notificationService)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _emailService = emailService;
            _firebaseUploadService = firebaseUploadService;
            _notificationService = notificationService;
        }

        public async Task<bool> CreateTripVehicleHandoverRecordAsync(TripVehicleHandoverRecordCreateDTO dto)
        {
            try
            {
                // 1. Kiểm tra Trip
                var trip = await _unitOfWork.TripRepo.GetByIdAsync(dto.TripId);
                if (trip == null) throw new Exception("Trip not found");

                // 2. Lấy Template biên bản xe mới nhất (Active)
                // Giả sử bạn có DeliveryRecordTemplateRepo
                var template = await _unitOfWork.DeliveryRecordTemplateRepo.GetAll()
                    .Include(t => t.DeliveryRecordTerms) // Include câu hỏi
                    .Where(t => t.Type == dto.Type && t.Status == DeliveryRecordTemplateStatus.ACTIVE)
                    .OrderByDescending(t => t.Version)
                    .FirstOrDefaultAsync();

                if (template == null) throw new Exception($"Không tìm thấy mẫu biên bản giao nhận xe cho loại {dto.Type}");

                // 3. Tạo Entity Record
                var newRecord = new TripVehicleHandoverRecord
                {
                    DeliveryRecordId = Guid.NewGuid(),
                    TripId = dto.TripId,
                    VehicleId = trip.VehicleId, // Lấy xe từ Trip
                    DeliveryRecordTemplateId = template.DeliveryRecordTemplateId,
                    Type = dto.Type, // PICKUP / DROPOFF
                    Status = DeliveryRecordStatus.PENDING, // Mới tạo là nháp

                    OwnerId = dto.HandoverUserId,
                    DriverId = dto.ReceiverUserId,

                    CreatedAt = TimeUtil.NowVN(),
                    Notes = dto.Notes,

                    // Khởi tạo các thông số mặc định bằng 0
                    CurrentOdometer = 0,
                    FuelLevel = 0,
                    IsEngineLightOn = false,

                    DriverSigned = false,
                    OwnerSigned = false,
                    DriverSignedAt = null,
                    OwnerSignedAt = null,
                    

                    // Copy Checklist từ Template sang Result
                    TermResults = new List<TripVehicleHandoverTermResult>()
                };

                // --- SỬA LOGIC GÁN ID TẠI ĐÂY ---
                if (dto.Type == DeliveryRecordType.HANDOVER) // Hoặc PICKUP (Giao xe: Chủ -> Tài)
                {
                    newRecord.OwnerId = dto.HandoverUserId; // Người giao là Chủ
                    newRecord.DriverId = dto.ReceiverUserId; // Người nhận là Tài
                }
                else if (dto.Type == DeliveryRecordType.RETURN) // Hoặc DROPOFF (Trả xe: Tài -> Chủ)
                {
                    newRecord.OwnerId = dto.ReceiverUserId;  // Người nhận là Chủ
                    newRecord.DriverId = dto.HandoverUserId; // Người giao là Tài
                }
                // --------------------------------

                // 4. Loop qua các điều khoản trong Template để tạo dòng check trống
                foreach (var term in template.DeliveryRecordTerms.OrderBy(t => t.DisplayOrder))
                {
                    newRecord.TermResults.Add(new TripVehicleHandoverTermResult
                    {
                        TripVehicleHandoverTermResultId = Guid.NewGuid(),
                        DeliveryRecordTermId = term.DeliveryRecordTermId, // Link về câu hỏi gốc
                        IsPassed = true, // Mặc định True (Đạt)
                        Note = "",
                        EvidenceImageUrl = null
                    });
                }

                // 5. Lưu xuống DB
                // Lưu ý: Nếu Repo của bạn là GenericRepo thì có thể cần cast hoặc dùng DbSet trực tiếp
                // Ở đây giả sử bạn đã khai báo DbSet<TripVehicleHandoverRecord> trong Context
                await _unitOfWork.TripVehicleHandoverRecordRepo.AddAsync(newRecord);

                // Vì hàm này có thể được gọi trong 1 Transaction lớn bên ngoài (TripDriverAssignmentService)
                // Nên ta KHÔNG gọi _unitOfWork.SaveChangeAsync() ở đây nếu muốn gộp transaction.
                // Tuy nhiên, để an toàn và hoạt động độc lập như DeliveryRecordService, ta save luôn.
                // Nếu muốn gộp transaction, bạn bỏ dòng dưới đi.
                await _unitOfWork.SaveChangeAsync();

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi tạo biên bản giao nhận xe: {ex.Message}");
            }
        }
        // ========================================================================
        // 1. GET BY ID (Lấy chi tiết biên bản + Checklist + Issues)
        // ========================================================================
        public async Task<ResponseDTO> GetByIdAsync(Guid recordId)
        {
            try
            {
                // Load Record kèm theo các bảng con
                var record = await _unitOfWork.TripVehicleHandoverRecordRepo.GetAll()
                    .Include(r => r.TermResults)
                        .ThenInclude(tr => tr.DeliveryRecordTerm) // Để lấy nội dung câu hỏi
                    .Include(r => r.Issues)
                        .ThenInclude(i => i.Images)
                    .Include(r => r.Owner) // Để lấy tên
                    .Include(r => r.Driver) // Để lấy tên
                    .FirstOrDefaultAsync(r => r.DeliveryRecordId == recordId);

                if (record == null)
                    return new ResponseDTO("Không tìm thấy biên bản giao nhận xe.", 404, false);

                // 2. [NEW] Load các khoản Bồi thường/Phạt liên quan đến xe của chuyến này
                // Logic: Lấy Surcharge cùng TripId VÀ có Type thuộc nhóm Hư hỏng/Vệ sinh xe
                var vehicleSurcharges = await _unitOfWork.TripSurchargeRepo.GetAll()
                    .Where(s => s.TripId == record.TripId && (
                        s.Type == SurchargeType.VEHICLE_DAMAGE ||
                        s.Type == SurchargeType.VEHICLE_DIRTY ||
                        s.Type == SurchargeType.SCRATCH ||
                        s.Type == SurchargeType.DENT ||
                        s.Type == SurchargeType.CRACK ||
                        s.Type == SurchargeType.PAINT_PEELING ||
                        s.Type == SurchargeType.DIRTY ||
                        s.Type == SurchargeType.ODOR ||
                        s.Type == SurchargeType.MECHANICAL ||
                        s.Type == SurchargeType.ELECTRICAL ||
                        s.Type == SurchargeType.TIRE ||
                        s.Type == SurchargeType.MISSING_ITEM
                    ))
                    .ToListAsync();

                // Map sang DTO
                var dto = new TripVehicleHandoverReadDTO
                {
                    TripVehicleHandoverRecordId = record.DeliveryRecordId,
                    TripId = record.TripId,
                    VehicleId = record.VehicleId,
                    Type = record.Type.ToString(),
                    Status = record.Status.ToString(),

                    HandoverUserId = record.OwnerId,
                    HandoverUserName = record.Owner.FullName ?? "Unknown",
                    ReceiverUserId = record.DriverId,
                    ReceiverUserName = record.Driver?.FullName ?? "Unknown",

                    CurrentOdometer = record.CurrentOdometer,
                    FuelLevel = record.FuelLevel,
                    IsEngineLightOn = record.IsEngineLightOn,
                    Notes = record.Notes,

                    HandoverSigned = record.OwnerSigned,
                    HandoverSignedAt = record.OwnerSignedAt,
                    //HandoverSignatureUrl = record.HandoverSignatureUrl,

                    ReceiverSigned = record.DriverSigned,
                    ReceiverSignedAt = record.DriverSignedAt,
                    //ReceiverSignatureUrl = record.ReceiverSignatureUrl,

                    // Map Checklist
                    TermResults = record.TermResults.Select(tr => new HandoverTermResultDTO
                    {
                        TripVehicleHandoverTermResultId = tr.TripVehicleHandoverTermResultId,
                        TermContent = tr.DeliveryRecordTerm?.Content ?? "Câu hỏi đã bị xóa",
                        IsPassed = tr.IsPassed,
                        Note = tr.Note,
                        EvidenceImageUrl = tr.EvidenceImageUrl
                    }).ToList(),

                    // Map Issues
                    Issues = record.Issues.Select(i => new HandoverIssueDTO
                    {
                        TripVehicleHandoverIssueId = i.TripVehicleHandoverIssueId,
                        IssueType = i.IssueType.ToString(),
                        Description = i.Description,
                        Status = i.Status.ToString(),
                        EstimatedCompensationAmount = i.EstimatedCompensationAmount,
                        ImageUrls = i.Images.Select(img => img.ImageUrl).ToList()
                    }).ToList(),
                    // [NEW] Map Surcharges (Khoản phạt chính thức đã tạo)
                    Surcharges = vehicleSurcharges.Select(s => new HandoverSurchargeDTO
                    {
                        TripSurchargeId = s.TripSurchargeId,
                        Type = s.Type.ToString(), // VD: SCRATCH, DENT
                        Amount = s.Amount,
                        Description = s.Description,
                        Status = s.Status.ToString() // PENDING / PAID
                    }).ToList()
                };

                return new ResponseDTO("Lấy thông tin thành công.", 200, true, dto);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi GetById: {ex.Message}", 500, false);
            }
        }

        // ========================================================================
        // 2. SEND OTP (Gửi mã xác thực cho người cần ký)
        // ========================================================================
        public async Task<ResponseDTO> SendOtpAsync(Guid recordId)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                // Lấy biên bản
                var record = await _unitOfWork.TripVehicleHandoverRecordRepo.GetByIdAsync(recordId);
                if (record == null) return new ResponseDTO("Biên bản không tồn tại.", 404, false);

                // Xác định người gọi API là Bên Giao hay Bên Nhận
                bool isHandoverSide = (record.OwnerId == userId);
                bool isReceiverSide = (record.DriverId == userId);

                if (!isHandoverSide && !isReceiverSide)
                    return new ResponseDTO("Bạn không liên quan đến biên bản này.", 403, false);

                // Kiểm tra xem đã ký chưa
                if (isHandoverSide && record.OwnerSignedAt != null && record.OwnerSigned)
                    return new ResponseDTO("Bạn đã ký biên bản này rồi.", 400, false);

                if (isReceiverSide && record.DriverSignedAt != null && record.DriverSigned)
                    return new ResponseDTO("Bạn đã ký biên bản này rồi.", 400, false);

                // Lấy thông tin user để gửi mail
                var user = await _unitOfWork.BaseUserRepo.GetByIdAsync(userId);

                // --- TẠO OTP ---
                string rawOtp = new Random().Next(100000, 999999).ToString();
                string hashedOtp = BCrypt.Net.BCrypt.HashPassword(rawOtp);

                // Revoke các token cũ
                var oldTokens = await _unitOfWork.UserTokenRepo.GetAll()
                    .Where(t => t.UserId == userId
                             && t.TokenType == TokenType.VEHICLE_HANDOVER_SIGNING_OTP // Dùng Enum riêng hoặc chung
                             && !t.IsRevoked)
                    .ToListAsync();
                foreach (var t in oldTokens) t.IsRevoked = true;

                // Lưu token mới
                var newToken = new UserToken
                {
                    UserTokenId = Guid.NewGuid(),
                    UserId = userId,
                    TokenType = TokenType.VEHICLE_HANDOVER_SIGNING_OTP,
                    TokenValue = hashedOtp,
                    CreatedAt = TimeUtil.NowVN(),
                    ExpiredAt = TimeUtil.NowVN().AddMinutes(5),
                    IsRevoked = false
                };
                await _unitOfWork.UserTokenRepo.AddAsync(newToken);

                if (oldTokens.Any()) _unitOfWork.UserTokenRepo.UpdateRange(oldTokens);
                await _unitOfWork.SaveChangeAsync();

                // Gửi Email
                string typeName = record.Type == DeliveryRecordType.PICKUP ? "GIAO XE" : "TRẢ XE";
                string recordCode = record.DeliveryRecordId.ToString().Substring(0, 8).ToUpper();

                await _emailService.SendContractSigningOtpAsync(user.Email, user.FullName, rawOtp, $"xe {typeName} - {recordCode}");

                return new ResponseDTO($"Mã OTP đã được gửi tới email {HideEmail(user.Email)}", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi gửi OTP: {ex.Message}", 500, false);
            }
        }

        // ========================================================================
        // 3. SIGN RECORD (Xác thực OTP & Ký tên) - [FIXED]
        // ========================================================================
        // ========================================================================
        // 3. SIGN RECORD (Xác thực OTP & Ký tên) - [FIXED & UPDATED]
        // ========================================================================
        public async Task<ResponseDTO> SignRecordAsync(SignVehicleHandoverDTO dto)
        {
            // [FIX] Sử dụng 'using' để quản lý Transaction Scope tự động
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                // --- BƯỚC 1: Validate OTP ---
                var validToken = await _unitOfWork.UserTokenRepo.GetAll()
                    .Where(t => t.UserId == userId
                              && t.TokenType == TokenType.VEHICLE_HANDOVER_SIGNING_OTP
                              && !t.IsRevoked
                              && t.ExpiredAt > TimeUtil.NowVN())
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (validToken == null || !BCrypt.Net.BCrypt.Verify(dto.Otp, validToken.TokenValue))
                    return new ResponseDTO("Mã OTP không hợp lệ hoặc đã hết hạn.", 400, false);

                // Hủy token sau khi dùng
                validToken.IsRevoked = true;
                await _unitOfWork.UserTokenRepo.UpdateAsync(validToken);

                // --- BƯỚC 2: Lấy Record & Trip ---
                var record = await _unitOfWork.TripVehicleHandoverRecordRepo.GetAll()
                    .Include(r => r.Trip) // Include Trip để update status
                    .FirstOrDefaultAsync(r => r.DeliveryRecordId == dto.RecordId);

                if (record == null) return new ResponseDTO("Biên bản không tồn tại.", 404, false);

                // --- BƯỚC 3: Xác định vai trò người ký ---
                bool isHandoverSide = (record.OwnerId == userId);
                bool isReceiverSide = (record.DriverId == userId);

                if (!isHandoverSide && !isReceiverSide)
                    return new ResponseDTO("Bạn không có quyền ký biên bản này.", 403, false);

                // --- BƯỚC 4: Thực hiện ký ---
                if (isHandoverSide)
                {
                    if (record.OwnerSignedAt != null && record.OwnerSigned) return new ResponseDTO("Bạn đã ký rồi.", 400, false);
                    record.OwnerSigned = true;
                    record.OwnerSignedAt = TimeUtil.NowVN();
                }
                else if (isReceiverSide)
                {
                    if (record.DriverSignedAt != null && record.DriverSigned) return new ResponseDTO("Bạn đã ký rồi.", 400, false);
                    record.DriverSigned = true;
                    record.DriverSignedAt = TimeUtil.NowVN();
                }

                // --- BƯỚC 5: Kiểm tra hoàn tất & Cập nhật Trạng Thái ---

                // [UPDATE] Logic Trạng Thái Record
                if (record.OwnerSignedAt != null && record.DriverSignedAt != null)
                {
                    // Cả 2 đã ký -> COMPLETED
                    record.Status = DeliveryRecordStatus.COMPLETED;

                    // --- CHỈ KHI COMPLETED MỚI UPDATE TRIP ---

                    // A. XỬ LÝ PICKUP (Giao xe cho tài xế)
                    if (record.Type == DeliveryRecordType.HANDOVER) // Sửa lại enum cho đúng
                    {
                        if (record.Trip.Status == TripStatus.VEHICLE_HANDOVERED)
                        {
                            record.Trip.Status = TripStatus.MOVING_TO_PICKUP;
                            await _unitOfWork.TripRepo.UpdateAsync(record.Trip);
                        }
                    }

                    // B. XỬ LÝ DROPOFF (Trả xe về bãi)
                    else if (record.Type == DeliveryRecordType.RETURN) // Sửa lại enum cho đúng
                    {
                        record.Trip.Status = TripStatus.DONE_TRIP_AND_WATING_FOR_PAYOUT;
                        record.Trip.ActualCompletedTime = TimeUtil.NowVN();
                        await _unitOfWork.TripRepo.UpdateAsync(record.Trip);

                        // CẬP NHẬT ODOMETER (Nếu cần)
                        var vehicle = await _unitOfWork.VehicleRepo.GetByIdAsync(record.VehicleId);
                        if (vehicle != null)
                        {
                            // vehicle.CurrentOdometer = record.CurrentOdometer;
                            // await _unitOfWork.VehicleRepo.UpdateAsync(vehicle);
                        }
                    }
                }
                else
                {
                    // [UPDATE] Mới có 1 người ký -> AWAITING_SIGNATURE
                    record.Status = DeliveryRecordStatus.AWAITING_DELIVERY_RECORD_SIGNATURE;
                }

                await _unitOfWork.TripVehicleHandoverRecordRepo.UpdateAsync(record);

                // Lưu & Commit
                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync(); // [FIX] Gọi qua biến transaction

                // [CHÈN VÀO ĐÂY]
                if (record.Status == DeliveryRecordStatus.COMPLETED)
                {
                    // Cả 2 đã ký -> Báo cho cả 2
                    _ = Task.Run(() => _notificationService.SendToUserAsync(record.OwnerId, "✅ Đã ký biên bản", "Biên bản giao nhận đã hoàn tất.", null));
                    _ = Task.Run(() => _notificationService.SendToUserAsync(record.DriverId, "✅ Đã ký biên bản", "Biên bản giao nhận đã hoàn tất.", null));
                }
                else
                {
                    // Mới 1 người ký -> Báo cho người kia
                    var targetId = (record.OwnerSigned == true) ? record.DriverId : record.OwnerId;
                    _ = Task.Run(() => _notificationService.SendToUserAsync(targetId, "✍️ Yêu cầu ký tên", "Đối tác đã ký biên bản. Vui lòng vào xác nhận.", null));
                }

                return new ResponseDTO("Ký biên bản thành công.", 200, true);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(); // [FIX] Gọi qua biến transaction
                return new ResponseDTO($"Lỗi hệ thống: {ex.Message}", 500, false);
            }
        }

        // Helper ẩn email
        private string HideEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return "";
            var parts = email.Split('@');
            if (parts.Length != 2) return email;
            if (parts[0].Length <= 2) return email;
            return $"{parts[0].Substring(0, 2)}***@{parts[1]}";
        }


        // ========================================================================
        // 4. UPDATE CHECKLIST (Lưu kết quả kiểm tra xe)
        // ========================================================================
        // Đảm bảo đã Inject Service này vào Constructor
        // private readonly IFirebaseUploadService _firebaseUploadService;

        public async Task<ResponseDTO> UpdateChecklistAsync(UpdateHandoverChecklistDTO dto)
        {
            try
            {
                // Load record và các TermResults
                var record = await _unitOfWork.TripVehicleHandoverRecordRepo.GetAll()
                    .Include(r => r.TermResults)
                    .FirstOrDefaultAsync(r => r.DeliveryRecordId == dto.RecordId);

                if (record == null) return new ResponseDTO("Biên bản không tồn tại.", 404, false);
                if (record.Status == DeliveryRecordStatus.COMPLETED)
                    return new ResponseDTO("Biên bản đã hoàn tất, không thể chỉnh sửa.", 400, false);

                // 1. Cập nhật thông tin chung
                record.CurrentOdometer = dto.CurrentOdometer;
                record.FuelLevel = dto.FuelLevel;
                record.IsEngineLightOn = dto.IsEngineLightOn;
                if (!string.IsNullOrEmpty(dto.Notes)) record.Notes = dto.Notes;

                // 2. Cập nhật từng dòng checklist
                foreach (var item in dto.ChecklistItems)
                {
                    var termResult = record.TermResults.FirstOrDefault(t => t.TripVehicleHandoverTermResultId == item.TripVehicleHandoverTermResultId);

                    if (termResult != null)
                    {
                        termResult.IsPassed = item.IsPassed;
                        termResult.Note = item.Note;

                        // --- LOGIC UPLOAD ẢNH ---
                        if (item.EvidenceImage != null && item.EvidenceImage.Length > 0)
                        {
                            // A. Xác định UserId sở hữu file 
                            // (Lấy từ record.DriverId hoặc ID của người đang đăng nhập thực hiện request này)
                            // Giả sử record có DriverId, nếu null thì dùng Guid.Empty hoặc ID mặc định
                            Guid uploadUserId = record.DriverId;

                            // B. Xác định Loại file (Enum FirebaseFileType)
                            // Bạn cần chọn Enum phù hợp trong code của bạn (ví dụ: Image, VehicleImage, Document...)
                            FirebaseFileType fileType = FirebaseFileType.HANDOVER_EVIDENCE_IMAGES;

                            // C. Gọi hàm Upload
                            string uploadedUrl = await _firebaseUploadService.UploadFileAsync(
                                item.EvidenceImage,
                                uploadUserId,
                                fileType
                            );

                            // D. Lưu URL trả về vào database
                            termResult.EvidenceImageUrl = uploadedUrl;
                        }
                        // --- KẾT THÚC LOGIC UPLOAD ---

                        await _unitOfWork.TripVehicleHandoverTermResultRepo.UpdateAsync(termResult);
                    }
                }

                await _unitOfWork.TripVehicleHandoverRecordRepo.UpdateAsync(record);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Cập nhật checklist thành công.", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi update checklist: {ex.Message}", 500, false);
            }
        }

        // ========================================================================
        // 5. REPORT ISSUE (Báo cáo sự cố mới)
        // ========================================================================
        // ========================================================================
        // 5. REPORT ISSUE (Báo cáo sự cố mới)
        // ========================================================================
        public async Task<ResponseDTO> ReportIssueAsync(ReportHandoverIssueDTO dto)
        {
            try
            {
                // 1. Kiểm tra biên bản tồn tại
                var record = await _unitOfWork.TripVehicleHandoverRecordRepo.GetByIdAsync(dto.RecordId);
                if (record == null) return new ResponseDTO("Biên bản không tồn tại.", 404, false);

                // 2. Khởi tạo đối tượng Issue
                var newIssue = new TripVehicleHandoverIssue
                {
                    TripVehicleHandoverIssueId = Guid.NewGuid(),
                    TripVehicleHandoverRecordId = dto.RecordId,
                    IssueType = dto.IssueType,
                    Description = dto.Description,
                    Status = IssueStatus.REPORTED, // Mặc định là mới báo cáo
                    CreatedAt = TimeUtil.NowVN(),
                    Images = new List<TripVehicleHandoverIssueImage>()
                };

                // 3. Xử lý Upload ảnh (Dùng Firebase Service)
                if (dto.Image != null && dto.Image.Length > 0)
                {
                    // Lấy UserId người đang thao tác để gom thư mục trên Firebase (nếu cần logic đó)
                    var userId = _userUtility.GetUserIdFromToken();
                    if (userId == Guid.Empty) userId = dto.RecordId; // Fallback nếu không lấy được User

                    // Gọi Service Upload
                    // Lưu ý: Hãy đảm bảo Enum FirebaseFileType.VEHICLE_ISSUE_IMAGE đã có, hoặc dùng loại tương đương
                    string uploadedUrl = await _firebaseUploadService.UploadFileAsync(dto.Image, userId, FirebaseFileType.HANDOVER_EVIDENCE_IMAGES);

                    if (!string.IsNullOrEmpty(uploadedUrl))
                    {
                        // Lưu URL vào danh sách ảnh của Issue
                        newIssue.Images.Add(new TripVehicleHandoverIssueImage
                        {
                            TripVehicleHandoverIssueImageId = Guid.NewGuid(),
                            TripVehicleHandoverIssueId = newIssue.TripVehicleHandoverIssueId,
                            ImageUrl = uploadedUrl,
                            Caption = dto.IssueType.ToString() // Caption mặc định là loại sự cố
                        });
                    }
                    else
                    {
                        return new ResponseDTO("Lỗi khi upload ảnh lên hệ thống.", 500, false);
                    }
                }

                // 4. Lưu xuống Database
                await _unitOfWork.TripVehicleHandoverIssueRepo.AddAsync(newIssue);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Báo cáo sự cố thành công.", 201, true, new { IssueId = newIssue.TripVehicleHandoverIssueId });
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi báo cáo sự cố: {ex.Message}", 500, false);
            }
        }
    }
}