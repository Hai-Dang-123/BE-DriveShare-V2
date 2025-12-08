using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
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
    public class TripDeliveryIssueService : ITripDeliveryIssueService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;
        private readonly IFirebaseUploadService _firebaseService;

        public TripDeliveryIssueService(IUnitOfWork unitOfWork, UserUtility userUtility, IFirebaseUploadService firebaseService)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _firebaseService = firebaseService;
        }

        // ========================================================================
        // 1. REPORT ISSUE (Dành cho User có tài khoản - Driver/Owner)
        // ========================================================================
        public async Task<ResponseDTO> ReportIssueAsync(TripDeliveryIssueCreateDTO dto)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                var tripExists = await _unitOfWork.TripRepo.AnyAsync(t => t.TripId == dto.TripId);
                if (!tripExists) return new ResponseDTO("Trip not found", 404, false);

                // Tạo Issue Entity
                var issue = new TripDeliveryIssue
                {
                    TripDeliveryIssueId = Guid.NewGuid(),
                    TripId = dto.TripId,
                    DeliveryRecordId = dto.DeliveryRecordId,
                    ReportedByUserId = userId,
                    IssueType = dto.IssueType,
                    Description = dto.Description,
                    Status = IssueStatus.REPORTED,
                    CreatedAt = DateTime.UtcNow,
                    DeliveryIssueImages = new List<TripDeliveryIssueImage>()
                };

                // [NEW] UPLOAD ẢNH LÊN FIREBASE & LƯU URL
                if (dto.Images != null && dto.Images.Count > 0)
                {
                    // Upload đồng thời (Parallel) để nhanh hơn
                    var uploadTasks = dto.Images.Select(async file =>
                    {
                        // Gọi Firebase Service (giả sử hàm UploadFileAsync trả về string URL)
                        // Tham số thứ 2 là ID người up (để tạo folder), thứ 3 là loại file
                        string imgUrl = await _firebaseService.UploadFileAsync(file, userId, FirebaseFileType.TRIPDELIVERY_ISSUE_IMAGES);
                        return imgUrl;
                    });

                    var imageUrls = await Task.WhenAll(uploadTasks);

                    // Lưu vào Entity
                    foreach (var url in imageUrls)
                    {
                        issue.DeliveryIssueImages.Add(new TripDeliveryIssueImage
                        {
                            TripDeliveryIssueImageId = Guid.NewGuid(),
                            TripDeliveryIssueId = issue.TripDeliveryIssueId,
                            ImageUrl = url,
                            Caption = dto.IssueType.ToString(),
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                await _unitOfWork.TripDeliveryIssueRepo.AddAsync(issue);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Báo cáo sự cố thành công.", 201, true, new { IssueId = issue.TripDeliveryIssueId });
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi báo cáo sự cố: {ex.Message}", 500, false);
            }
        }

        // ========================================================================
        // [NEW] REPORT ISSUE FOR CONTACT (Dành cho Khách vãng lai)
        // ========================================================================
        public async Task<ResponseDTO> ReportIssueForContactAsync(TripDeliveryIssueCreateDTO dto, string accessToken)
        {
            try
            {
                // 1. Validate Token & Quyền
                var validAccess = await _unitOfWork.ContactTokenRepo.GetAll()
                    .Include(t => t.TripContact)
                    .FirstOrDefaultAsync(t => t.TokenValue == accessToken
                                           && t.TokenType == TokenType.VIEW_ACCESS_TOKEN
                                           && !t.IsRevoked
                                           && t.ExpiredAt > DateTime.UtcNow);

                if (validAccess == null) return new ResponseDTO("Phiên làm việc hết hạn.", 401, false);

                if (!dto.DeliveryRecordId.HasValue)
                    return new ResponseDTO("Thiếu thông tin biên bản giao hàng (DeliveryRecordId).", 400, false);

                var record = await _unitOfWork.TripDeliveryRecordRepo.GetByIdAsync(dto.DeliveryRecordId.Value);
                if (record == null) return new ResponseDTO("Biên bản không tồn tại.", 404, false);

                if (record.TripContactId != validAccess.TripContactId)
                    return new ResponseDTO("Bạn không có quyền báo cáo sự cố cho đơn hàng này.", 403, false);

                if (record.Status == DeliveryRecordStatus.COMPLETED || record.ContactSigned == true)
                    return new ResponseDTO("Biên bản đã hoàn tất, không thể báo cáo thêm.", 400, false);

                // 2. Tạo Issue
                var issue = new TripDeliveryIssue
                {
                    TripDeliveryIssueId = Guid.NewGuid(),
                    TripId = dto.TripId,
                    DeliveryRecordId = dto.DeliveryRecordId.Value,
                    ReportedByUserId = null,
                    ReportedByContactId = validAccess.TripContactId,
                    IssueType = dto.IssueType,
                    Description = $"[Khách: {validAccess.TripContact.FullName}] {dto.Description}",
                    Status = IssueStatus.REPORTED,
                    CreatedAt = DateTime.UtcNow,
                    DeliveryIssueImages = new List<TripDeliveryIssueImage>()
                };

                // [NEW] UPLOAD ẢNH LÊN FIREBASE
                if (dto.Images != null && dto.Images.Count > 0)
                {
                    var uploadTasks = dto.Images.Select(async file =>
                    {
                        // Với khách vãng lai, ta dùng ID của TripContact để làm folder name cho gọn
                        string imgUrl = await _firebaseService.UploadFileAsync(file, validAccess.TripContactId, FirebaseFileType.TRIPDELIVERY_ISSUE_IMAGES);
                        return imgUrl;
                    });

                    var imageUrls = await Task.WhenAll(uploadTasks);

                    foreach (var url in imageUrls)
                    {
                        issue.DeliveryIssueImages.Add(new TripDeliveryIssueImage
                        {
                            TripDeliveryIssueImageId = Guid.NewGuid(),
                            TripDeliveryIssueId = issue.TripDeliveryIssueId,
                            ImageUrl = url,
                            Caption = dto.IssueType.ToString(),
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                await _unitOfWork.TripDeliveryIssueRepo.AddAsync(issue);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Khách hàng báo cáo sự cố thành công.", 201, true, new { IssueId = issue.TripDeliveryIssueId });
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi: {ex.Message}", 500, false);
            }
        }

        // ========================================================================
        // 2. GET ISSUES BY TRIP (Update Mapping cho trường hợp Contact báo)
        // ========================================================================
        public async Task<ResponseDTO> GetIssuesByTripIdAsync(Guid tripId)
        {
            try
            {
                var issues = await _unitOfWork.TripDeliveryIssueRepo.GetAll()
                    .Include(i => i.DeliveryIssueImages)
                    .Include(i => i.ReportedByUser)
                    .Include(i => i.ReportedByContact) // Include thêm bảng Contact
                    .Include(i => i.Surcharges)
                    .Where(i => i.TripId == tripId)
                    .OrderByDescending(i => i.CreatedAt)
                    .ToListAsync(); // Lấy về RAM trước để xử lý null check dễ hơn

                var dtoList = issues.Select(i => new TripDeliveryIssueReadDTO
                {
                    TripDeliveryIssueId = i.TripDeliveryIssueId,
                    TripId = i.TripId,
                    DeliveryRecordId = i.DeliveryRecordId,

                    ReportedByUserId = i.ReportedByUserId,
                    // Logic lấy tên người báo: Ưu tiên User, nếu null thì lấy Contact
                    ReportedByUserName = i.ReportedByUser?.FullName ?? i.ReportedByContact?.FullName ?? "Unknown",

                    IssueType = i.IssueType.ToString(),
                    Description = i.Description,
                    Status = i.Status.ToString(),
                    CreatedAt = i.CreatedAt,
                    ImageUrls = i.DeliveryIssueImages.Select(img => img.ImageUrl).ToList(),
                    Surcharges = i.Surcharges.Select(s => new IssueSurchargeDTO
                    {
                        TripSurchargeId = s.TripSurchargeId,
                        Amount = s.Amount,
                        Description = s.Description
                    }).ToList()
                }).ToList();

                return new ResponseDTO("Lấy danh sách sự cố thành công.", 200, true, dtoList);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi lấy danh sách: {ex.Message}", 500, false);
            }
        }

        // ========================================================================
        // 3. GET ISSUE BY ID (Update Mapping)
        // ========================================================================
        public async Task<ResponseDTO> GetIssueByIdAsync(Guid issueId)
        {
            try
            {
                var i = await _unitOfWork.TripDeliveryIssueRepo.GetAll()
                    .Include(i => i.DeliveryIssueImages)
                    .Include(i => i.ReportedByUser)
                    .Include(i => i.ReportedByContact) // Include Contact
                    .Include(i => i.Surcharges)
                    .FirstOrDefaultAsync(x => x.TripDeliveryIssueId == issueId);

                if (i == null) return new ResponseDTO("Không tìm thấy sự cố.", 404, false);

                var dto = new TripDeliveryIssueReadDTO
                {
                    TripDeliveryIssueId = i.TripDeliveryIssueId,
                    TripId = i.TripId,
                    DeliveryRecordId = i.DeliveryRecordId,
                    ReportedByUserId = i.ReportedByUserId,
                    // Logic lấy tên người báo
                    ReportedByUserName = i.ReportedByUser?.FullName ?? i.ReportedByContact?.FullName ?? "Unknown",

                    IssueType = i.IssueType.ToString(),
                    Description = i.Description,
                    Status = i.Status.ToString(),
                    CreatedAt = i.CreatedAt,
                    ImageUrls = i.DeliveryIssueImages.Select(img => img.ImageUrl).ToList(),
                    Surcharges = i.Surcharges.Select(s => new IssueSurchargeDTO
                    {
                        TripSurchargeId = s.TripSurchargeId,
                        Amount = s.Amount,
                        Description = s.Description
                    }).ToList()
                };

                return new ResponseDTO("Lấy thông tin thành công.", 200, true, dto);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi lấy chi tiết: {ex.Message}", 500, false);
            }
        }

        // ========================================================================
        // 4. UPDATE STATUS
        // ========================================================================
        public async Task<ResponseDTO> UpdateIssueStatusAsync(UpdateIssueStatusDTO dto)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                // TODO: Check Role nếu cần

                var issue = await _unitOfWork.TripDeliveryIssueRepo.GetByIdAsync(dto.TripDeliveryIssueId);
                if (issue == null) return new ResponseDTO("Sự cố không tồn tại.", 404, false);

                issue.Status = dto.NewStatus;
                await _unitOfWork.TripDeliveryIssueRepo.UpdateAsync(issue);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Cập nhật trạng thái thành công.", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi cập nhật trạng thái: {ex.Message}", 500, false);
            }
        }
    }
}