using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.DTOs.TripVehicleHandoverRecord;
using Common.Enums.Status;
using Common.Enums.Type;
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

        public TripVehicleHandoverRecordService(IUnitOfWork unitOfWork, UserUtility userUtility, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _emailService = emailService;
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

                    CreatedAt = DateTime.UtcNow,
                    Notes = dto.Notes,

                    // Khởi tạo các thông số mặc định bằng 0
                    CurrentOdometer = 0,
                    FuelLevel = 0,
                    IsEngineLightOn = false,

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
                    CreatedAt = DateTime.UtcNow,
                    ExpiredAt = DateTime.UtcNow.AddMinutes(5),
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
        // 3. SIGN RECORD (Xác thực OTP & Ký tên)
        // ========================================================================
        public async Task<ResponseDTO> SignRecordAsync(SignVehicleHandoverDTO dto)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                // --- BƯỚC 1: Validate OTP ---
                var validToken = await _unitOfWork.UserTokenRepo.GetAll()
                    .Where(t => t.UserId == userId
                             && t.TokenType == TokenType.VEHICLE_HANDOVER_SIGNING_OTP
                             && !t.IsRevoked
                             && t.ExpiredAt > DateTime.UtcNow)
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
                    record.OwnerSignedAt = DateTime.UtcNow;
                }
                else if (isReceiverSide)
                {
                    if (record.DriverSignedAt != null && record.OwnerSigned) return new ResponseDTO("Bạn đã ký rồi.", 400, false);
                    record.DriverSignedAt = DateTime.UtcNow;
                    record.OwnerSigned = true;
                }

                // --- BƯỚC 5: Kiểm tra hoàn tất & Cập nhật Trip/Vehicle ---
                if (record.OwnerSignedAt != null && record.DriverSignedAt != null)
                {
                    record.Status = DeliveryRecordStatus.COMPLETED;

                    // A. XỬ LÝ PICKUP (Giao xe cho tài xế)
                    if (record.Type == DeliveryRecordType.PICKUP)
                    {
                        // Logic: Chuyển Trip sang MOVING_TO_PICKUP (hoặc ON_TRIP tùy flow)
                        // Ý nghĩa: Tài xế đã nhận xe và bắt đầu đi
                        if (record.Trip.Status == TripStatus.VEHICLE_HANDOVER )
                        {
                            record.Trip.Status = TripStatus.MOVING_TO_PICKUP;
                            await _unitOfWork.TripRepo.UpdateAsync(record.Trip);
                        }
                    }

                    // B. XỬ LÝ DROPOFF (Trả xe về bãi)
                    else if (record.Type == DeliveryRecordType.DROPOFF)
                    {
                        // Logic: Chuyển Trip sang COMPLETED
                        record.Trip.Status = TripStatus.COMPLETED;
                        record.Trip.ActualCompletedTime = DateTime.UtcNow;
                        await _unitOfWork.TripRepo.UpdateAsync(record.Trip);

                        // CẬP NHẬT ODOMETER CHO XE (QUAN TRỌNG)
                        //var vehicle = await _unitOfWork.VehicleRepo.GetByIdAsync(record.VehicleId);
                        //if (vehicle != null)
                        //{
                        //    // Chỉ update nếu số mới lớn hơn số cũ (để an toàn)
                        //    if (record.CurrentOdometer > vehicle.Odometer)
                        //    {
                        //        vehicle.Odometer = record.CurrentOdometer;
                        //        await _unitOfWork.VehicleRepo.UpdateAsync(vehicle);
                        //    }
                        //    // Cập nhật vị trí xe về vị trí trả
                        //    // vehicle.Location = ... (nếu record có lưu Location)
                        //}
                    }
                }

                await _unitOfWork.TripVehicleHandoverRecordRepo.UpdateAsync(record);

                await _unitOfWork.SaveChangeAsync(); // Save các thay đổi OTP, Record
                await _unitOfWork.CommitTransactionAsync(); // Commit

                return new ResponseDTO("Ký biên bản thành công.", 200, true);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
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
        public async Task<ResponseDTO> UpdateChecklistAsync(UpdateHandoverChecklistDTO dto)
        {
            try
            {
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
                        termResult.EvidenceImageUrl = item.EvidenceImageUrl;

                        // Update trạng thái Entity (nếu cần thiết với EF Core tracking)
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
        public async Task<ResponseDTO> ReportIssueAsync(ReportHandoverIssueDTO dto)
        {
            try
            {
                var record = await _unitOfWork.TripVehicleHandoverRecordRepo.GetByIdAsync(dto.RecordId);
                if (record == null) return new ResponseDTO("Biên bản không tồn tại.", 404, false);

                // Tạo Issue
                var newIssue = new TripVehicleHandoverIssue
                {
                    TripVehicleHandoverIssueId = Guid.NewGuid(),
                    TripVehicleHandoverRecordId = dto.RecordId,
                    IssueType = dto.IssueType,
                    Description = dto.Description,
                    Status = IssueStatus.REPORTED, // Mặc định là mới báo cáo
                    CreatedAt = DateTime.UtcNow,
                    Images = new List<TripVehicleHandoverIssueImage>()
                };

                // Thêm ảnh
                if (dto.ImageUrls != null && dto.ImageUrls.Any())
                {
                    foreach (var url in dto.ImageUrls)
                    {
                        newIssue.Images.Add(new TripVehicleHandoverIssueImage
                        {
                            TripVehicleHandoverIssueImageId = Guid.NewGuid(),
                            TripVehicleHandoverIssueId = newIssue.TripVehicleHandoverIssueId,
                            ImageUrl = url,
                            Caption = dto.IssueType.ToString()
                        });
                    }
                }

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