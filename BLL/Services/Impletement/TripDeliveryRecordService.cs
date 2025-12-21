using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
using DAL.Entities;
using DAL.Repositories.Interface;
using DAL.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class TripDeliveryRecordService : ITripDeliveryRecordService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFirebaseUploadService _firebaseUploadService;
        private readonly UserUtility _userUtility;   
        private readonly IEmailService _emailService;
        public TripDeliveryRecordService(IUnitOfWork unitOfWork, UserUtility userUtility, IFirebaseUploadService firebaseUploadService, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _firebaseUploadService = firebaseUploadService;
            _emailService = emailService;
        }
        // create trip delivery record
        public async Task<ResponseDTO> CreateTripDeliveryRecordAsync(TripDeliveryRecordCreateDTO tripDeliveryRecordDTO)
        {
            try
            {
                var userid = _userUtility.GetUserIdFromToken();
                if (userid == Guid.Empty)
                {
                    return new ResponseDTO
                    {
                        IsSuccess = false,
                        StatusCode = StatusCodes.Status401Unauthorized,
                        Message = "Unauthorized: User ID not found in token."
                    };
                }
                var newTripDeliveryRecord = new TripDeliveryRecord
            {
                DeliveryRecordId = Guid.NewGuid(),
                TripId = tripDeliveryRecordDTO.TripId,
                DeliveryRecordTemplateId = tripDeliveryRecordDTO.DeliveryRecordTempalteId,
                TripContactId = tripDeliveryRecordDTO.StripContractId,
                DriverId = userid,
                CreatedAt = TimeUtil.NowVN(),
                Notes = tripDeliveryRecordDTO.Notes,
                Type = tripDeliveryRecordDTO.type,
                Status = DeliveryRecordStatus.PENDING
                };
                await _unitOfWork.TripDeliveryRecordRepo.AddAsync(newTripDeliveryRecord);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO
                {
                    IsSuccess = true,
                    Message = "Trip Delivery Record created successfully",
                };
            } catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    Message = $"Error creating Trip Delivery Record: {ex.Message}"
                };
            }
        }
        // get trip delivery record by id
        public async Task<ResponseDTO?> GetByIdAsync(Guid tripDeliveryRecordId)
        {
            try
            {
                var record = await _unitOfWork.TripDeliveryRecordRepo.GetByIdWithDetailsAsync(tripDeliveryRecordId);

                if (record == null)
                    return new ResponseDTO { 
                        IsSuccess = false, 
                        Message = "No record found for this TripDeliveryRecordId." 
                    };

                var dto = new TripDeliveryRecordReadDTO
                {
                    TripId = record.TripId,
                    DeliveryRecordTempalteId = record.DeliveryRecordTemplateId,
                    type = record.Type.ToString(),
                    Status = record.Status.ToString(),
                    Notes = record.Notes,
                    DriverSignatureUrl = record.DriverSignatureUrl,
                    DriverSignedAt = record.DriverSignedAt,
                    ContactSignatureUrl = record.ContactSignatureUrl,
                    ContactSignedAt = record.ContactSignedAt,

                    tripContact = record.TripContact != null
                        ? new TripContactDTO
                        {
                            TripContactId = record.TripContact.TripContactId,
                            Type = record.TripContact.Type.ToString(),
                            FullName = record.TripContact.FullName,
                            PhoneNumber = record.TripContact.PhoneNumber,
                            Note = record.TripContact.Note
                        }
                        : null,

                    deliveryRecordTemplate = record.DeliveryRecordTemplate != null
                        ? new DeliveryRecordTemplateDTO
                        {
                            DeliveryRecordTemplateId = record.DeliveryRecordTemplate.DeliveryRecordTemplateId,
                            TemplateName = record.DeliveryRecordTemplate.TemplateName,
                            Version = record.DeliveryRecordTemplate.Version,
                            Type = record.DeliveryRecordTemplate.Type.ToString(),
                            CreatedAt = record.DeliveryRecordTemplate.CreatedAt,
                            DeliveryRecordTerms = record.DeliveryRecordTemplate.DeliveryRecordTerms
                                .Select(term => new DeliveryRecordTermDTO
                                {
                                    DeliveryRecordTermId = term.DeliveryRecordTermId,
                                    Content = term.Content,
                                    DisplayOrder = term.DisplayOrder
                                }).ToList()
                        }
                        : null
                };

                return new ResponseDTO
                {
                    IsSuccess = true,
                    Message = "Trip delivery records retrieved successfully.",
                    Result = dto
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    Message = $"Error retrieving Trip Delivery Record: {ex.Message}"
                };
            }
        }
        // get trip delivery records by trip id
        public async Task<ResponseDTO> GetByTripIdAsync(Guid tripId)
        {
            try
            {
                var records = await _unitOfWork.TripDeliveryRecordRepo.GetByTripIdAsync(tripId);

                if (records == null || !records.Any())
                {
                    return new ResponseDTO
                    {
                        IsSuccess = false,
                        StatusCode = StatusCodes.Status404NotFound,
                        Message = "No Trip Delivery Records found for this TripId."
                    };
                }

                var dtoList = records.Select(r => new TripDeliveryRecordReadDTO
                {
                    TripId = r.TripId,
                    DeliveryRecordTempalteId = r.DeliveryRecordTemplateId,
                    StripContractId = r.TripContactId,
                    type = r.Type.ToString(),
                    Status = r.Status.ToString(),
                    Notes = r.Notes,
                    DriverSignatureUrl = r.DriverSignatureUrl,
                    DriverSignedAt = r.DriverSignedAt,
                    ContactSignatureUrl = r.ContactSignatureUrl,
                    ContactSignedAt = r.ContactSignedAt,

                    tripContact = r.TripContact != null
                        ? new TripContactDTO
                        {
                            TripContactId = r.TripContact.TripContactId,
                            Type = r.TripContact.Type.ToString(),
                            FullName = r.TripContact.FullName,
                            PhoneNumber = r.TripContact.PhoneNumber,
                            Note = r.TripContact.Note
                        }
                        : null,

                    deliveryRecordTemplate = r.DeliveryRecordTemplate != null
                        ? new DeliveryRecordTemplateDTO
                        {
                            DeliveryRecordTemplateId = r.DeliveryRecordTemplate.DeliveryRecordTemplateId,
                            TemplateName = r.DeliveryRecordTemplate.TemplateName,
                            Version = r.DeliveryRecordTemplate.Version,
                            Type = r.DeliveryRecordTemplate.Type.ToString(),
                            CreatedAt = r.DeliveryRecordTemplate.CreatedAt,
                            DeliveryRecordTerms = r.DeliveryRecordTemplate.DeliveryRecordTerms
                                .Select(term => new DeliveryRecordTermDTO
                                {
                                    DeliveryRecordTermId = term.DeliveryRecordTermId,
                                    Content = term.Content,
                                    DisplayOrder = term.DisplayOrder
                                }).ToList()
                        }
                        : null
                }).ToList();

                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Trip Delivery Records retrieved successfully.",
                    Result = dtoList
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = $"Error retrieving Trip Delivery Records: {ex.Message}"
                };
            }

        }
        // sign delivery record
        //public async Task<ResponseDTO> SignDeliveryRecordAsync(Guid tripDeliveryRecordId)
        //{
        //    try
        //    {
        //        var userId = _userUtility.GetUserIdFromToken();
        //        if (userId == Guid.Empty)
        //            return new ResponseDTO("Unauthorized", 401, false);

        //        var record = await _unitOfWork.TripDeliveryRecordRepo.GetByIdWithDetailsAsync(tripDeliveryRecordId);
        //        if (record == null)
        //            return new ResponseDTO("Delivery record not found", 404, false);

        //        bool isDriver = record.DriverId == userId;
        //        bool isContact = record.TripContactId == userId;

        //        if (!isDriver && !isContact)
        //            return new ResponseDTO("You are not authorized to sign this delivery record", 403, false);

        //        // --- Ký biên bản ---
        //        if (isDriver)
        //        {
        //            if (record.DriverSigned == true)
        //                return new ResponseDTO("Driver already signed", 400, false);

        //            record.DriverSigned = true;
        //            record.DriverSignedAt = DateTime.UtcNow;
        //        }
        //        else if (isContact)
        //        {
        //            if (record.ContactSigned == true)
        //                return new ResponseDTO("Contact already signed", 400, false);

        //            record.ContactSigned = true;
        //            record.ContactSignedAt = DateTime.UtcNow;
        //        }

        //        // --- Cập nhật trạng thái biên bản ---
        //        if (record.DriverSigned == true && record.ContactSigned == true)
        //        {
        //            record.Status = DeliveryRecordStatus.COMPLETED;

        //        }
        //        else
        //        {
        //            // Nếu 1 bên đã ký thì chuyển sang trạng thái "IN_PROGRESS"
        //            record.Status = DeliveryRecordStatus.AWAITING_DELIVERY_RECORD_SIGNATURE;
        //        }

        //        await _unitOfWork.TripDeliveryRecordRepo.UpdateAsync(record);
        //        await _unitOfWork.SaveChangeAsync();

        //        return new ResponseDTO{
        //            IsSuccess = true,
        //            Message = "Delivery record signed successfully",
        //            StatusCode = StatusCodes.Status200OK                   
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        return new ResponseDTO
        //        {
        //            IsSuccess = false,
        //            Message = $"Error signing Delivery Record: {ex.Message}"
        //        };
        //    }
        //}

        public async Task<ResponseDTO> SignDeliveryRecordAsync(SignDeliveryRecordDTO dto)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO("Unauthorized", 401, false);

                // ========================================================================
                // 🛡️ BƯỚC 0: XÁC THỰC OTP (QUAN TRỌNG)
                // ========================================================================

                // 1. Tìm Token OTP hợp lệ
                var validToken = await _unitOfWork.UserTokenRepo.GetAll()
                    .Where(t => t.UserId == userId
                             && t.TokenType == TokenType.DELIVERY_RECORD_SIGNING_OTP // Enum OTP riêng hoặc dùng chung CONTRACT_SIGNING_OTP tùy bạn
                             && !t.IsRevoked
                             && t.ExpiredAt > TimeUtil.NowVN())
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (validToken == null)
                {
                    return new ResponseDTO("Mã OTP không tồn tại hoặc đã hết hạn.", 400, false);
                }

                // 2. Verify mã OTP
                bool isOtpCorrect = BCrypt.Net.BCrypt.Verify(dto.Otp, validToken.TokenValue);
                if (!isOtpCorrect)
                {
                    return new ResponseDTO("Mã OTP không chính xác.", 400, false);
                }

                // 3. Hủy hiệu lực Token
                validToken.IsRevoked = true;
                await _unitOfWork.UserTokenRepo.UpdateAsync(validToken);


                // ========================================================================
                // 📝 BƯỚC TIẾP THEO: KÝ BIÊN BẢN
                // ========================================================================

                // 1. Lấy Record và Trip
                var record = await _unitOfWork.TripDeliveryRecordRepo.GetAll()
                     .Include(r => r.Trip) // Include Trip để update status
                     .FirstOrDefaultAsync(r => r.DeliveryRecordId == dto.DeliveryRecordId);

                if (record == null)
                    return new ResponseDTO("Delivery record not found", 404, false);

                // 2. Validate Quyền
                bool isDriver = record.DriverId == userId;
                bool isContact = record.TripContactId == userId;

                if (!isDriver && !isContact)
                    return new ResponseDTO("You are not authorized to sign this record", 403, false);

                // 3. Thực hiện ký
                if (isDriver)
                {
                    if (record.DriverSigned == true) return new ResponseDTO("Driver already signed", 400, false);
                    record.DriverSigned = true;
                    record.DriverSignedAt = TimeUtil.NowVN();
                }
                else if (isContact)
                {
                    if (record.ContactSigned == true) return new ResponseDTO("Contact already signed", 400, false);
                    record.ContactSigned = true;
                    record.ContactSignedAt = TimeUtil.NowVN();
                }

                // 4. Cập nhật trạng thái Biên bản
                if (record.DriverSigned == true && record.ContactSigned == true)
                {
                    record.Status = DeliveryRecordStatus.COMPLETED;

                    // ⚠️ 5. CẬP NHẬT TRẠNG THÁI TRIP (NẾU CẢ 2 ĐÃ KÝ)
                    if (record.Trip != null)
                    {
                        // Nếu là biên bản PICKUP (Lấy hàng) -> Chuyển sang ĐANG GIAO (IN_TRANSIT)
                        if (record.Type == DeliveryRecordType.PICKUP)
                        {
                            // Logic: Nếu trip đang ở trạng thái "Đang đến lấy" hoặc "Đã đến lấy"
                            if (record.Trip.Status == TripStatus.MOVING_TO_PICKUP || record.Trip.Status == TripStatus.LOADING)
                            {
                                record.Trip.Status = TripStatus.MOVING_TO_DROPOFF;
                                record.Trip.ActualPickupTime = TimeUtil.NowVN(); // Ghi nhận giờ lấy thực tế
                                await _unitOfWork.TripRepo.UpdateAsync(record.Trip);
                            }
                        }
                        // Nếu là biên bản DROPOFF (Giao hàng) -> Chuyển sang HOÀN THÀNH (COMPLETED)
                        else if (record.Type == DeliveryRecordType.DROPOFF)
                        {
                            // Logic: Nếu trip đang ở trạng thái "Đang giao" hoặc "Đã đến giao"
                            if (record.Trip.Status == TripStatus.MOVING_TO_DROPOFF || record.Trip.Status == TripStatus.UNLOADING)
                            {
                                // Nếu còn tiền chưa thanh toán -> AWAITING_FINAL_PAYOUT
                                // Nếu xong xuôi -> COMPLETED
                                record.Trip.Status = TripStatus.READY_FOR_VEHICLE_RETURN;
                                record.Trip.ActualCompletedTime = TimeUtil.NowVN(); // Ghi nhận giờ giao thực tế
                                await _unitOfWork.TripRepo.UpdateAsync(record.Trip);
                            }
                        }
                    }
                }
                else
                {
                    record.Status = DeliveryRecordStatus.AWAITING_DELIVERY_RECORD_SIGNATURE;
                }

                await _unitOfWork.TripDeliveryRecordRepo.UpdateAsync(record);

                // 6. Save Change (Transaction OTP + Record + Trip)
                await _unitOfWork.SaveChangeAsync();



                return new ResponseDTO("Delivery record signed successfully", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error signing Delivery Record: {ex.Message}", 500, false);
            }
        }

        // Trong TripDeliveryRecordService.cs

        public async Task<ResponseDTO> SendOTPToSignDeliveryRecordAsync(Guid recordId)
        {
            try
            {
                // 1. Lấy User
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                var user = await _unitOfWork.BaseUserRepo.GetByIdAsync(userId);

                // 2. Lấy Biên bản
                var record = await _unitOfWork.TripDeliveryRecordRepo.GetByIdAsync(recordId);
                if (record == null) return new ResponseDTO("Record not found", 404, false);

                // 3. Check quyền (Chỉ Driver hoặc Contact của biên bản này mới được lấy OTP)
                bool isDriver = record.DriverId == userId;
                bool isContact = record.TripContactId == userId;

                if (!isDriver && !isContact)
                    return new ResponseDTO("You are not related to this record", 403, false);

                // 4. Check đã ký chưa
                if (isDriver && record.DriverSigned == true) return new ResponseDTO("You already signed this record", 400, false);
                if (isContact && record.ContactSigned == true) return new ResponseDTO("You already signed this record", 400, false);

                // 5. Tạo OTP
                string rawOtp = new Random().Next(100000, 999999).ToString();
                string hashedOtp = BCrypt.Net.BCrypt.HashPassword(rawOtp);

                // 6. Xử lý Token cũ (Revoke)
                // Lưu ý: Dùng Enum DELIVERY_RECORD_SIGNING_OTP để phân biệt với Hợp đồng
                var oldTokens = await _unitOfWork.UserTokenRepo.GetAll()
                    .Where(t => t.UserId == userId
                             && t.TokenType == TokenType.DELIVERY_RECORD_SIGNING_OTP
                             && !t.IsRevoked)
                    .ToListAsync();

                foreach (var t in oldTokens) t.IsRevoked = true;

                // 7. Lưu Token mới
                var newToken = new UserToken
                {
                    UserTokenId = Guid.NewGuid(),
                    UserId = userId,
                    TokenType = TokenType.DELIVERY_RECORD_SIGNING_OTP, // <-- Quan trọng
                    TokenValue = hashedOtp,
                    CreatedAt = TimeUtil.NowVN(),
                    ExpiredAt = TimeUtil.NowVN().AddMinutes(5),
                    IsRevoked = false
                };

                await _unitOfWork.UserTokenRepo.AddAsync(newToken);
                if (oldTokens.Any()) _unitOfWork.UserTokenRepo.UpdateRange(oldTokens);
                await _unitOfWork.SaveChangeAsync();

                // 8. Gửi Email
                // Bạn có thể viết thêm hàm SendDeliveryOtpAsync trong EmailService cho nội dung khác biệt
                string recordCode = record.DeliveryRecordId.ToString().Substring(0, 8).ToUpper();
                await _emailService.SendContractSigningOtpAsync(user.Email, user.FullName, rawOtp, $"BB-{recordCode}");

                return new ResponseDTO($"Mã OTP ký biên bản đã gửi tới {HideEmail(user.Email)}", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error sending OTP: {ex.Message}", 500, false);
            }
        }

        private string HideEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return "";
            var parts = email.Split('@');
            if (parts.Length != 2) return email;
            if (parts[0].Length <= 2) return email;

            return $"{parts[0].Substring(0, 1)}***@{parts[1]}";
        }

        public async Task CreateTripDeliveryRecordAsync(TripDeliveryRecordCreateDTO dto, Guid driverId)
        {
            try
            {
                // Validate DriverId (service gọi đến phải đảm bảo driverId hợp lệ)
                if (driverId == Guid.Empty)
                {
                    throw new ArgumentException("Invalid DriverId provided.", nameof(driverId));
                }

                var newTripDeliveryRecord = new TripDeliveryRecord
                {
                    DeliveryRecordId = Guid.NewGuid(),
                    TripId = dto.TripId,
                    DeliveryRecordTemplateId = dto.DeliveryRecordTempalteId,
                    TripContactId = dto.StripContractId,
                    DriverId = driverId, // ⚠️ SỬA ĐỔI: Lấy từ tham số
                    CreatedAt = TimeUtil.NowVN(),
                    Notes = dto.Notes,
                    Type = dto.type,
                    Status = DeliveryRecordStatus.PENDING
                };

                await _unitOfWork.TripDeliveryRecordRepo.AddAsync(newTripDeliveryRecord);

                // ⚠️ LƯU Ý: 
                // Hàm này không gọi SaveChangeAsync() hoặc CommitTransaction().
                // Hàm GỌI (ví dụ: TripDriverAssignmentService) sẽ chịu trách nhiệm
                // Commit Transaction sau khi gọi hàm này.
            }
            catch (Exception ex)
            {
                // Ghi log lỗi (nếu có logger)
                // _logger.LogError(ex, "Error creating Trip Delivery Record internal.");

                // ⚠️ SỬA ĐỔI: Ném Exception để hàm gọi xử lý Rollback
                throw new Exception($"Error creating Trip Delivery Record: {ex.Message}", ex);
            }
        }

        public async Task<ResponseDTO> GetAllAsync(int pageNumber, int pageSize)
        {
            try
            {
                // 1. Lấy thông tin User
                var userId = _userUtility.GetUserIdFromToken();
                var userRole = _userUtility.GetUserRoleFromToken();
                if (userId == Guid.Empty)
                {
                    return new ResponseDTO
                    {
                        IsSuccess = false,
                        StatusCode = StatusCodes.Status401Unauthorized,
                        Message = "Unauthorized: Invalid token"
                    };
                }

                // 2. Lấy IQueryable cơ sở (Giả định có .GetAll() trả về IQueryable)
                var query = _unitOfWork.TripDeliveryRecordRepo.GetAll()
                                     .AsNoTracking();

                // 3. Lọc theo Vai trò (Authorization)
                if (userRole == "Admin")
                {
                    // Admin: không cần lọc, thấy tất cả
                }
                else if (userRole == "Owner")
                {
                    // Owner: Lọc theo các Trip thuộc sở hữu của họ
                    // (Cần Include Trip để lọc)
                    query = query.Where(r => r.Trip.OwnerId == userId);
                }
                else if (userRole == "Driver")
                {
                    // Driver: Lọc theo các record gán cho họ
                    query = query.Where(r => r.DriverId == userId);
                }
                else
                {
                    // Các vai trò khác không có quyền xem
                    return new ResponseDTO
                    {
                        IsSuccess = false,
                        StatusCode = StatusCodes.Status403Forbidden,
                        Message = "Forbidden: You do not have permission to access all records."
                    };
                }

                // 4. Include các dữ liệu liên quan (giống hệt GetByTripIdAsync)
                query = query
                    .Include(r => r.TripContact)
                    .Include(r => r.DeliveryRecordTemplate)
                        .ThenInclude(t => t.DeliveryRecordTerms);

                // 5. Đếm tổng số lượng (sau khi lọc)
                var totalCount = await query.CountAsync();

                // 6. Lấy dữ liệu của trang và Map sang DTO
                var records = await query
                    .OrderByDescending(r => r.CreatedAt) // Sắp xếp
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(r => new TripDeliveryRecordReadDTO // Dùng DTO y hệt GetByTripId
                    {
                        TripId = r.TripId,
                        DeliveryRecordTempalteId = r.DeliveryRecordTemplateId,
                        StripContractId = r.TripContactId,
                        type = r.Type.ToString(),
                        Status = r.Status.ToString(),
                        Notes = r.Notes,
                        DriverSignatureUrl = r.DriverSignatureUrl,
                        DriverSignedAt = r.DriverSignedAt,
                        ContactSignatureUrl = r.ContactSignatureUrl,
                        ContactSignedAt = r.ContactSignedAt,

                        tripContact = r.TripContact != null
                            ? new TripContactDTO
                            {
                                TripContactId = r.TripContact.TripContactId,
                                Type = r.TripContact.Type.ToString(),
                                FullName = r.TripContact.FullName,
                                PhoneNumber = r.TripContact.PhoneNumber,
                                Note = r.TripContact.Note
                            }
                            : null,

                        deliveryRecordTemplate = r.DeliveryRecordTemplate != null
                            ? new DeliveryRecordTemplateDTO
                            {
                                DeliveryRecordTemplateId = r.DeliveryRecordTemplate.DeliveryRecordTemplateId,
                                TemplateName = r.DeliveryRecordTemplate.TemplateName,
                                Version = r.DeliveryRecordTemplate.Version,
                                Type = r.DeliveryRecordTemplate.Type.ToString(),
                                CreatedAt = r.DeliveryRecordTemplate.CreatedAt,
                                DeliveryRecordTerms = r.DeliveryRecordTemplate.DeliveryRecordTerms
                                    .Select(term => new DeliveryRecordTermDTO
                                    {
                                        DeliveryRecordTermId = term.DeliveryRecordTermId,
                                        Content = term.Content,
                                        DisplayOrder = term.DisplayOrder
                                    }).ToList()
                            }
                            : null
                    })
                    .ToListAsync();

                // 7. Tạo kết quả PaginatedDTO
                var paginatedResult = new PaginatedDTO<TripDeliveryRecordReadDTO>(records, totalCount, pageNumber, pageSize);

                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Trip Delivery Records retrieved successfully.",
                    Result = paginatedResult
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = $"Error retrieving Trip Delivery Records: {ex.Message}"
                };
            }
        }

        public async Task<ResponseDTO> GetByIdForDriverAsync(Guid tripDeliveryRecordId)
        {
            try
            {
                // Đảm bảo Repository đã Include: Trip, Trip.Packages, Trip.Packages.Item, DeliveryRecordTemplate.Terms, TripContact
                var record = await _unitOfWork.TripDeliveryRecordRepo.GetByIdWithDetailsForDriverAsync(tripDeliveryRecordId);

                if (record == null)
                    return new ResponseDTO
                    {
                        IsSuccess = false,
                        Message = "No record found for this TripDeliveryRecordId.",
                        StatusCode = 404
                    };

                var dto = new TripDeliveryRecordReadForDriverDTO
                {
                    TripDeliveryRecordId = record.DeliveryRecordId, // ID của bản ghi
                    TripId = record.TripId,
                    DeliveryRecordTempalteId = record.DeliveryRecordTemplateId,
                    Type = record.Type.ToString(),
                    Status = record.Status.ToString(),
                    Notes = record.Notes,
                    //DriverSignatureUrl = record.DriverSignatureUrl,
                    DriverSigned = record.DriverSigned,
                    ContactSigned = record.ContactSigned,
                    DriverSignedAt = record.DriverSignedAt,
                    //ContactSignatureUrl = record.ContactSignatureUrl,
                    ContactSignedAt = record.ContactSignedAt,

                    // 1. Mapping Trip Contact
                    TripContact = record.TripContact != null
                        ? new TripContactForDriverDTO
                        {
                            TripContactId = record.TripContact.TripContactId,
                            Type = record.TripContact.Type.ToString(),
                            FullName = record.TripContact.FullName,
                            PhoneNumber = record.TripContact.PhoneNumber,
                            Note = record.TripContact.Note
                        }
                        : null,

                    DriverPrimary = record.Driver != null
                        ? new TripDriverAssignmentForDriverDTO
                        {
                            DriverId = record.Driver.UserId,
                            FullName = record.Driver.FullName,
                            PhoneNumber = record.Driver.PhoneNumber,
                            
                        }
                        : null,

                    // 2. Mapping Template & Terms
                    DeliveryRecordTemplate = record.DeliveryRecordTemplate != null
                        ? new DeliveryRecordTemplateForDriverDTO
                        {
                            DeliveryRecordTemplateId = record.DeliveryRecordTemplate.DeliveryRecordTemplateId,
                            TemplateName = record.DeliveryRecordTemplate.TemplateName,
                            Version = record.DeliveryRecordTemplate.Version,
                            Type = record.DeliveryRecordTemplate.Type.ToString(),
                            CreatedAt = record.DeliveryRecordTemplate.CreatedAt,
                            DeliveryRecordTerms = record.DeliveryRecordTemplate.DeliveryRecordTerms
                                .OrderBy(t => t.DisplayOrder) // Nên sắp xếp theo thứ tự hiển thị
                                .Select(term => new DeliveryRecordTermForDriverDTO
                                {
                                    DeliveryRecordTermId = term.DeliveryRecordTermId,
                                    Content = term.Content,
                                    DisplayOrder = term.DisplayOrder
                                }).ToList()
                        }
                        : null,

                    // [NEW] 4. MAPPING ISSUES & IMAGES
                    Issues = record.Issues.Select(i => new DeliveryIssueForDriverDTO
                    {
                        TripDeliveryIssueId = i.TripDeliveryIssueId,
                        IssueType = i.IssueType.ToString(),
                        Description = i.Description,
                        Status = i.Status.ToString(),
                        CreatedAt = i.CreatedAt,

                        // Map Images
                        ImageUrls = i.DeliveryIssueImages
                                     .Select(img => img.ImageUrl)
                                     .ToList(),
                        // [NEW] Map Bồi thường / Phạt
                        Surcharges = i.Surcharges.Select(s => new IssueSurchargeForDriverDTO
                        {
                            TripSurchargeId = s.TripSurchargeId,
                            Type = s.Type.ToString(),
                            Amount = s.Amount,
                            Description = s.Description,
                            Status = s.Status.ToString()
                        }).ToList()
                    }).ToList(),

                    // 3. Mapping Trip, Package & Items (QUAN TRỌNG)
                    TripDetail = record.Trip != null ? new TripDetailForRecordForDriverDTO
                    {
                        TripCode = record.Trip.TripCode,
                        Status = record.Trip.Status.ToString(),
                        Type = record.Trip.Type.ToString(),
                        Packages = record.Trip.Packages.Select(pkg => new PackageDetailForDriverDTO
                        {
                            PackageId = pkg.PackageId,
                            PackageCode = pkg.PackageCode,
                            Title = pkg.Title,
                            Description = pkg.Description,
                            Quantity = pkg.Quantity,
                            Unit = pkg.Unit,
                            WeightKg = pkg.WeightKg,
                            VolumeM3 = pkg.VolumeM3,
                           

                            // Map Item
                            Item = pkg.Item != null ? new ItemDetailForDriverDTO
                            {
                                ItemId = pkg.Item.ItemId,
                                Name = pkg.Item.ItemName,          // Giả định Item có property này
                                ImageUrls = pkg.Item.ItemImages.Select(img => img.ItemImageURL).ToList()
                            } : null,

                            // Map Images nếu có
                            ImageUrls = pkg.PackageImages.Select(img => img.PackageImageURL).ToList()
                        }).ToList()
                    } : null
                };

                return new ResponseDTO
                {
                    IsSuccess = true,
                    Message = "Trip delivery record retrieved successfully with full details.",
                    Result = dto,
                    StatusCode = 200
                };
            }
            catch (Exception ex)
            {
                // Log ex here
                return new ResponseDTO
                {
                    IsSuccess = false,
                    Message = $"Error retrieving Trip Delivery Record: {ex.Message}",
                    StatusCode = 500
                };
            }
        }


        // ========================================================================
        // 1. GET DELIVERY RECORD FOR CONTACT (Validate Access Token từ URL)
        // ========================================================================
        public async Task<ResponseDTO> GetDeliveryRecordForContactAsync(Guid recordId, string accessToken)
        {
            try
            {
                // A. Validate Access Token (Token dài trên URL email)
                // Tìm trong bảng ContactToken xem có token truy cập này không
                var validAccess = await _unitOfWork.ContactTokenRepo.GetAll()
                    .FirstOrDefaultAsync(t => t.TokenValue == accessToken // Token dài không cần hash (hoặc hash tùy logic bạn)
                                           && t.TokenType == TokenType.VIEW_ACCESS_TOKEN // Enum mới cho quyền xem
                                           && t.ExpiredAt > TimeUtil.NowVN());

                if (validAccess == null)
                {
                    return new ResponseDTO("Link đã hết hạn hoặc không hợp lệ.", 401, false);
                }

                // B. Lấy thông tin biên bản
                // Kiểm tra xem token này có đúng là của Contact trong biên bản này không
                var record = await _unitOfWork.TripDeliveryRecordRepo.GetByIdWithDetailsForDriverAsync(recordId);

                if (record == null) return new ResponseDTO("Biên bản không tồn tại.", 404, false);

                if (record.TripContactId != validAccess.TripContactId)
                    return new ResponseDTO("Token không khớp với người nhận trong biên bản này.", 403, false);

                // C. Map sang DTO (Ẩn các thông tin nhạy cảm nếu cần)
                var dto = new TripDeliveryRecordReadForDriverDTO
                {
                    TripDeliveryRecordId = record.DeliveryRecordId, // ID của bản ghi
                    TripId = record.TripId,
                    DeliveryRecordTempalteId = record.DeliveryRecordTemplateId,
                    Type = record.Type.ToString(),
                    Status = record.Status.ToString(),
                    Notes = record.Notes,
                    //DriverSignatureUrl = record.DriverSignatureUrl,
                    DriverSigned = record.DriverSigned,
                    ContactSigned = record.ContactSigned,
                    DriverSignedAt = record.DriverSignedAt,
                    //ContactSignatureUrl = record.ContactSignatureUrl,
                    ContactSignedAt = record.ContactSignedAt,

                    // 1. Mapping Trip Contact
                    TripContact = record.TripContact != null
                        ? new TripContactForDriverDTO
                        {
                            TripContactId = record.TripContact.TripContactId,
                            Type = record.TripContact.Type.ToString(),
                            FullName = record.TripContact.FullName,
                            PhoneNumber = record.TripContact.PhoneNumber,
                            Note = record.TripContact.Note
                        }
                        : null,

                    DriverPrimary = record.Driver != null
                        ? new TripDriverAssignmentForDriverDTO
                        {
                            DriverId = record.Driver.UserId,
                            FullName = record.Driver.FullName,
                            PhoneNumber = record.Driver.PhoneNumber,

                        }
                        : null,

                    // 2. Mapping Template & Terms
                    DeliveryRecordTemplate = record.DeliveryRecordTemplate != null
                        ? new DeliveryRecordTemplateForDriverDTO
                        {
                            DeliveryRecordTemplateId = record.DeliveryRecordTemplate.DeliveryRecordTemplateId,
                            TemplateName = record.DeliveryRecordTemplate.TemplateName,
                            Version = record.DeliveryRecordTemplate.Version,
                            Type = record.DeliveryRecordTemplate.Type.ToString(),
                            CreatedAt = record.DeliveryRecordTemplate.CreatedAt,
                            DeliveryRecordTerms = record.DeliveryRecordTemplate.DeliveryRecordTerms
                                .OrderBy(t => t.DisplayOrder) // Nên sắp xếp theo thứ tự hiển thị
                                .Select(term => new DeliveryRecordTermForDriverDTO
                                {
                                    DeliveryRecordTermId = term.DeliveryRecordTermId,
                                    Content = term.Content,
                                    DisplayOrder = term.DisplayOrder
                                }).ToList()
                        }
                        : null,

                    // [NEW] MAPPING ISSUES
                    Issues = record.Issues.Select(i => new DeliveryIssueForDriverDTO
                    {
                        TripDeliveryIssueId = i.TripDeliveryIssueId,
                        IssueType = i.IssueType.ToString(),
                        Description = i.Description,
                        Status = i.Status.ToString(),
                        CreatedAt = i.CreatedAt,
                        ImageUrls = i.DeliveryIssueImages.Select(img => img.ImageUrl).ToList(),
                        // [NEW] Map Bồi thường / Phạt
                        Surcharges = i.Surcharges.Select(s => new IssueSurchargeForDriverDTO
                        {
                            TripSurchargeId = s.TripSurchargeId,
                            Type = s.Type.ToString(),
                            Amount = s.Amount,
                            Description = s.Description,
                            Status = s.Status.ToString()
                        }).ToList()
                    }).ToList(),

                    // 3. Mapping Trip, Package & Items (QUAN TRỌNG)
                    TripDetail = record.Trip != null ? new TripDetailForRecordForDriverDTO
                    {
                        TripCode = record.Trip.TripCode,
                        Status = record.Trip.Status.ToString(),
                        Type = record.Trip.Type.ToString(),
                        Packages = record.Trip.Packages.Select(pkg => new PackageDetailForDriverDTO
                        {
                            PackageId = pkg.PackageId,
                            PackageCode = pkg.PackageCode,
                            Title = pkg.Title,
                            Description = pkg.Description,
                            Quantity = pkg.Quantity,
                            Unit = pkg.Unit,
                            WeightKg = pkg.WeightKg,
                            VolumeM3 = pkg.VolumeM3,
                           

                            // Map Item
                            Item = pkg.Item != null ? new ItemDetailForDriverDTO
                            {
                                ItemId = pkg.Item.ItemId,
                                Name = pkg.Item.ItemName,          // Giả định Item có property này
                                ImageUrls = pkg.Item.ItemImages.Select(img => img.ItemImageURL).ToList()
                            } : null,

                            // Map Images nếu có
                            ImageUrls = pkg.PackageImages.Select(img => img.PackageImageURL).ToList()
                        }).ToList()
                    } : null
                };

                return new ResponseDTO("Lấy thông tin biên bản thành công", 200, true, dto);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi: {ex.Message}", 500, false);
            }
        }

        // ========================================================================
        // 2. SEND OTP TO CONTACT (Gửi mã 6 số để ký)
        // ========================================================================
        public async Task<ResponseDTO> SendOTPToContactAsync(Guid recordId, string accessToken)
        {
            try
            {
                // A. Validate Access Token lần nữa (để đảm bảo đúng người đang thao tác)
                var validAccess = await _unitOfWork.ContactTokenRepo.GetAll()
                    .FirstOrDefaultAsync(t => t.TokenValue == accessToken
                                           && t.TokenType == TokenType.VIEW_ACCESS_TOKEN
                                           && t.ExpiredAt > TimeUtil.NowVN());

                if (validAccess == null) return new ResponseDTO("Phiên làm việc hết hạn, vui lòng tải lại trang từ email.", 401, false);

                // B. Lấy Record & Contact
                var record = await _unitOfWork.TripDeliveryRecordRepo.GetByIdAsync(recordId);
                if (record == null) return new ResponseDTO("Record not found", 404, false);

                if (record.ContactSigned == true) return new ResponseDTO("Bạn đã ký biên bản này rồi.", 400, false);

                var contact = await _unitOfWork.TripContactRepo.GetByIdAsync(validAccess.TripContactId);
                if (contact == null || string.IsNullOrEmpty(contact.Email))
                    return new ResponseDTO("Không tìm thấy Email của người liên hệ này.", 400, false);

                // C. Tạo OTP & Lưu vào bảng ContactToken
                string rawOtp = new Random().Next(100000, 999999).ToString();
                string hashedOtp = BCrypt.Net.BCrypt.HashPassword(rawOtp);

                // Hủy các OTP cũ của contact này
                var oldOtps = await _unitOfWork.ContactTokenRepo.GetAll()
                    .Where(t => t.TripContactId == contact.TripContactId && t.TokenType == TokenType.DELIVERY_RECORD_SIGNING_OTP)
                    .ToListAsync();
                foreach (var t in oldOtps) t.IsRevoked = true;

                var otpToken = new ContactToken
                {
                    ContactTokenId = Guid.NewGuid(),
                    TripContactId = contact.TripContactId,
                    TokenValue = hashedOtp,
                    TokenType = TokenType.DELIVERY_RECORD_SIGNING_OTP,
                    CreatedAt = TimeUtil.NowVN(),
                    ExpiredAt = TimeUtil.NowVN().AddMinutes(10),
                    IsRevoked = false
                };

                await _unitOfWork.ContactTokenRepo.AddAsync(otpToken);
                if (oldOtps.Any()) _unitOfWork.ContactTokenRepo.UpdateRange(oldOtps);
                await _unitOfWork.SaveChangeAsync();

                // D. Gửi Email
                await _emailService.SendContractSigningOtpAsync(contact.Email, contact.FullName, rawOtp, $"BB-{recordId.ToString()[..8]}");

                // Trả về email đã che để FE hiển thị
                return new ResponseDTO($"Mã OTP đã được gửi tới {HideEmail(contact.Email)}", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi gửi OTP: {ex.Message}", 500, false);
            }
        }

        // ========================================================================
        // 3. SIGN DELIVERY RECORD (CONTACT)
        // ========================================================================
        public async Task<ResponseDTO> SignDeliveryRecordForContactAsync(Guid recordId, string otp, string accessToken)
        {
            try
            {
                // A. Validate Access Token (Token xem)
                var validAccess = await _unitOfWork.ContactTokenRepo.GetAll()
                   .FirstOrDefaultAsync(t => t.TokenValue == accessToken
                                          && t.TokenType == TokenType.VIEW_ACCESS_TOKEN
                                          && t.ExpiredAt > TimeUtil.NowVN());
                if (validAccess == null) return new ResponseDTO("Access Token không hợp lệ.", 401, false);

                // B. Validate OTP (Token ký) - Query bảng ContactToken
                var validOtp = await _unitOfWork.ContactTokenRepo.GetAll()
                    .Where(t => t.TripContactId == validAccess.TripContactId
                             && t.TokenType == TokenType.DELIVERY_RECORD_SIGNING_OTP
                             && !t.IsRevoked
                             && t.ExpiredAt > TimeUtil.NowVN())
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (validOtp == null || !BCrypt.Net.BCrypt.Verify(otp, validOtp.TokenValue))
                {
                    return new ResponseDTO("Mã OTP không chính xác hoặc đã hết hạn.", 400, false);
                }

                // C. Revoke OTP
                validOtp.IsRevoked = true;
                await _unitOfWork.ContactTokenRepo.UpdateAsync(validOtp);

                // D. Thực hiện Ký (Logic giống Driver)
                var record = await _unitOfWork.TripDeliveryRecordRepo.GetAll()
                    .Include(r => r.Trip)
                    .FirstOrDefaultAsync(r => r.DeliveryRecordId == recordId);

                if (record == null) return new ResponseDTO("Record not found", 404, false);

                if (record.ContactSigned == true) return new ResponseDTO("Bạn đã ký rồi.", 200, true);

                record.ContactSigned = true;
                record.ContactSignedAt = TimeUtil.NowVN();

                // E. Check trạng thái hoàn thành & Update Trip Status
                if (record.DriverSigned == true && record.ContactSigned == true)
                {
                    record.Status = DeliveryRecordStatus.COMPLETED;

                    if (record.Trip != null)
                    {
                        // --- UPDATE TRIP STATUS LOGIC ---

                        // 1. Nếu là Lấy hàng (PICKUP) xong
                        if (record.Type == DeliveryRecordType.PICKUP)
                        {
                            // Từ MOVING_TO_PICKUP/LOADING -> MOVING_TO_DROPOFF
                            if (record.Trip.Status == TripStatus.MOVING_TO_PICKUP || record.Trip.Status == TripStatus.LOADING)
                            {
                                record.Trip.Status = TripStatus.MOVING_TO_DROPOFF; // ✅ Cập nhật theo yêu cầu
                                record.Trip.ActualPickupTime = TimeUtil.NowVN();
                                await _unitOfWork.TripRepo.UpdateAsync(record.Trip);
                            }
                        }
                        // 2. Nếu là Giao hàng (DROPOFF) xong
                        else if (record.Type == DeliveryRecordType.DROPOFF)
                        {
                            // Từ MOVING_TO_DROPOFF/UNLOADING -> COMPLETED
                            if (record.Trip.Status == TripStatus.MOVING_TO_DROPOFF || record.Trip.Status == TripStatus.UNLOADING)
                            {
                                record.Trip.Status = TripStatus.READY_FOR_VEHICLE_RETURN; // Hoặc RETURNING_VEHICLE
                                record.Trip.ActualCompletedTime = TimeUtil.NowVN();
                                await _unitOfWork.TripRepo.UpdateAsync(record.Trip);
                            }
                        }
                    }
                }
                else
                {
                    // Mới có 1 bên ký
                    record.Status = DeliveryRecordStatus.AWAITING_DELIVERY_RECORD_SIGNATURE;
                }

                await _unitOfWork.TripDeliveryRecordRepo.UpdateAsync(record);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Ký biên bản thành công!", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi hệ thống: {ex.Message}", 500, false);
            }
        }

        // BLL/Services/Impletement/TripDeliveryRecordService.cs

        public async Task<ResponseDTO> SendAccessLinkToContactAsync(Guid recordId)
        {
            try
            {
                // 1. Lấy Record & Contact
                var record = await _unitOfWork.TripDeliveryRecordRepo.GetByIdAsync(recordId);
                if (record == null) return new ResponseDTO("Record not found", 404, false);

                // Nếu đã ký rồi thì thôi không gửi nữa (hoặc tùy logic bạn)
                if (record.ContactSigned == true) return new ResponseDTO("Record already signed", 200, true);

                var contact = await _unitOfWork.TripContactRepo.GetByIdAsync(record.TripContactId);
                if (contact == null || string.IsNullOrEmpty(contact.Email))
                    // Nếu không có email thì bỏ qua, không lỗi, chỉ return false message
                    return new ResponseDTO("Contact has no email address.", 400, false);

                // 2. Tạo Access Token (Token dài để xem)
                string accessToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

                var tokenEntity = new ContactToken
                {
                    ContactTokenId = Guid.NewGuid(),
                    TripContactId = contact.TripContactId,
                    TokenValue = accessToken,
                    TokenType = TokenType.VIEW_ACCESS_TOKEN,
                    CreatedAt = TimeUtil.NowVN(),
                    ExpiredAt = TimeUtil.NowVN().AddDays(7), // Link sống 7 ngày
                    IsRevoked = false
                };

                await _unitOfWork.ContactTokenRepo.AddAsync(tokenEntity);
                await _unitOfWork.SaveChangeAsync();

                // 3. Tạo Link (Thay đổi domain theo FE của bạn)
                // Ví dụ: https://driveshare.vn/delivery-record/view/{id}?token={token}
                string frontendUrl = "http://localhost:8081"; // Hoặc Domain thật
                string link = $"{frontendUrl}/contact-v2/DeliveryRecordScreen?recordId={recordId}&accessToken={accessToken}";

                // 4. Gửi Email
                string typeName = record.Type == DeliveryRecordType.PICKUP ? "GIAO NHẬN (LẤY HÀNG)" : "BÀN GIAO (TRẢ HÀNG)";
                string recordCode = record.DeliveryRecordId.ToString().Substring(0, 8).ToUpper();

                await _emailService.SendDeliveryRecordLinkEmailAsync(contact.Email, contact.FullName, link, recordCode, typeName);

                return new ResponseDTO("Email sent to contact", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error sending link: {ex.Message}", 500, false);
            }
        }

    }
}
