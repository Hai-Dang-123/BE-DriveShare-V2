using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.AspNetCore.Http;
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

        public TripDeliveryIssueService(IUnitOfWork unitOfWork, UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
        }

        // ========================================================================
        // 1. REPORT ISSUE (Báo cáo sự cố hàng hóa)
        // ========================================================================
        public async Task<ResponseDTO> ReportIssueAsync(TripDeliveryIssueCreateDTO dto)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                // Kiểm tra Trip tồn tại
                var tripExists = await _unitOfWork.TripRepo.AnyAsync(t => t.TripId == dto.TripId);
                if (!tripExists) return new ResponseDTO("Trip not found", 404, false);

                // Tạo Entity Issue
                var issue = new TripDeliveryIssue
                {
                    TripDeliveryIssueId = Guid.NewGuid(),
                    TripId = dto.TripId,
                    DeliveryRecordId = dto.DeliveryRecordId,
                    ReportedByUserId = userId,
                    IssueType = dto.IssueType,
                    Description = dto.Description,
                    Status = IssueStatus.REPORTED, // Trạng thái ban đầu
                    CreatedAt = DateTime.UtcNow,
                    DeliveryIssueImages = new List<TripDeliveryIssueImage>()
                };

                // Thêm ảnh minh chứng
                if (dto.ImageUrls != null && dto.ImageUrls.Any())
                {
                    foreach (var url in dto.ImageUrls)
                    {
                        issue.DeliveryIssueImages.Add(new TripDeliveryIssueImage
                        {
                            TripDeliveryIssueImageId = Guid.NewGuid(),
                            TripDeliveryIssueId = issue.TripDeliveryIssueId,
                            ImageUrl = url,
                            Caption = dto.IssueType.ToString()
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
        // 2. GET BY TRIP ID (Lấy danh sách sự cố của 1 chuyến đi)
        // ========================================================================
        public async Task<ResponseDTO> GetIssuesByTripIdAsync(Guid tripId)
        {
            try
            {
                var issues = await _unitOfWork.TripDeliveryIssueRepo.GetAll()
                    .Include(i => i.DeliveryIssueImages)
                    .Include(i => i.ReportedByUser)
                    .Include(i => i.Surcharges) // Include để xem có bị phạt tiền chưa
                    .Where(i => i.TripId == tripId)
                    .OrderByDescending(i => i.CreatedAt)
                    .Select(i => new TripDeliveryIssueReadDTO
                    {
                        TripDeliveryIssueId = i.TripDeliveryIssueId,
                        TripId = i.TripId,
                        DeliveryRecordId = i.DeliveryRecordId,
                        ReportedByUserId = i.ReportedByUserId,
                        ReportedByUserName = i.ReportedByUser.FullName,
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
                    })
                    .ToListAsync();

                return new ResponseDTO("Lấy danh sách sự cố thành công.", 200, true, issues);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi lấy danh sách: {ex.Message}", 500, false);
            }
        }

        // ========================================================================
        // 3. GET BY ID (Xem chi tiết 1 sự cố)
        // ========================================================================
        public async Task<ResponseDTO> GetIssueByIdAsync(Guid issueId)
        {
            try
            {
                var i = await _unitOfWork.TripDeliveryIssueRepo.GetAll()
                    .Include(i => i.DeliveryIssueImages)
                    .Include(i => i.ReportedByUser)
                    .Include(i => i.Surcharges)
                    .FirstOrDefaultAsync(x => x.TripDeliveryIssueId == issueId);

                if (i == null) return new ResponseDTO("Không tìm thấy sự cố.", 404, false);

                var dto = new TripDeliveryIssueReadDTO
                {
                    TripDeliveryIssueId = i.TripDeliveryIssueId,
                    TripId = i.TripId,
                    DeliveryRecordId = i.DeliveryRecordId,
                    ReportedByUserId = i.ReportedByUserId,
                    ReportedByUserName = i.ReportedByUser.FullName,
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
        // 4. UPDATE STATUS (Duyệt/Xử lý sự cố)
        // ========================================================================
        public async Task<ResponseDTO> UpdateIssueStatusAsync(UpdateIssueStatusDTO dto)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                // TODO: Thêm check role Admin hoặc Owner ở đây nếu cần thiết

                var issue = await _unitOfWork.TripDeliveryIssueRepo.GetByIdAsync(dto.TripDeliveryIssueId);
                if (issue == null) return new ResponseDTO("Sự cố không tồn tại.", 404, false);

                issue.Status = dto.NewStatus;

                // Nếu trạng thái là RESOLVED hoặc CANCELLED, có thể logic thêm ở đây (vd: gửi noti)

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