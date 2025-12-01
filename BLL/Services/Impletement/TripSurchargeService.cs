using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class TripSurchargeService : ITripSurchargeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;

        public TripSurchargeService(IUnitOfWork unitOfWork, UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
        }

        // ========================================================================
        // 1. TẠO PHỤ PHÍ (Create Surcharge)
        // ========================================================================
        public async Task<ResponseDTO> CreateSurchargeAsync(TripSurchargeCreateDTO dto)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                // 1. Validate Trip & Quyền (Chỉ Owner của Trip mới được tạo phạt)
                var trip = await _unitOfWork.TripRepo.GetByIdAsync(dto.TripId);
                if (trip == null) return new ResponseDTO("Trip not found", 404, false);

                if (trip.OwnerId != userId)
                    return new ResponseDTO("Bạn không có quyền tạo phụ phí cho chuyến đi này.", 403, false);

                // 2. Validate Issue (Nếu có truyền ID issue thì phải check issue đó thuộc Trip này)
                if (dto.TripVehicleHandoverIssueId.HasValue)
                {
                    var issue = await _unitOfWork.TripVehicleHandoverIssueRepo.GetByIdAsync(dto.TripVehicleHandoverIssueId.Value);
                    // Lưu ý: Issue -> Record -> Trip. Cần query kỹ hơn nếu muốn chắc chắn.
                    // Ở đây tạm check null
                    if (issue == null) return new ResponseDTO("Sự cố xe không tồn tại.", 400, false);
                }

                if (dto.TripDeliveryIssueId.HasValue)
                {
                    var issue = await _unitOfWork.TripDeliveryIssueRepo.GetByIdAsync(dto.TripDeliveryIssueId.Value);
                    if (issue == null || issue.TripId != dto.TripId)
                        return new ResponseDTO("Sự cố hàng hóa không hợp lệ.", 400, false);
                }

                // 3. Tạo Entity
                var surcharge = new TripSurcharge
                {
                    TripSurchargeId = Guid.NewGuid(),
                    TripId = dto.TripId,
                    Type = dto.Type,
                    Amount = dto.Amount,
                    Description = dto.Description,
                    Status = SurchargeStatus.PENDING, // Mặc định là chưa trả

                    TripVehicleHandoverIssueId = dto.TripVehicleHandoverIssueId,
                    TripDeliveryIssueId = dto.TripDeliveryIssueId,

                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.TripSurchargeRepo.AddAsync(surcharge);
                await _unitOfWork.SaveChangeAsync();

                // TODO: Có thể bắn Notification cho Driver: "Bạn vừa bị phạt 500k vì làm hỏng xe"

                return new ResponseDTO("Tạo phụ phí thành công.", 201, true, new { SurchargeId = surcharge.TripSurchargeId });
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi tạo phụ phí: {ex.Message}", 500, false);
            }
        }

        // ========================================================================
        // 2. LẤY DANH SÁCH PHỤ PHÍ THEO TRIP
        // ========================================================================
        public async Task<ResponseDTO> GetSurchargesByTripIdAsync(Guid tripId)
        {
            try
            {
                var surcharges = await _unitOfWork.TripSurchargeRepo.GetAll()
                    .Where(s => s.TripId == tripId)
                    .OrderByDescending(s => s.CreatedAt)
                    .Select(s => new TripSurchargeReadDTO
                    {
                        TripSurchargeId = s.TripSurchargeId,
                        TripId = s.TripId,
                        Type = s.Type.ToString(),
                        Amount = s.Amount,
                        Description = s.Description,
                        Status = s.Status.ToString(),
                        CreatedAt = s.CreatedAt,
                        PaidAt = s.PaidAt,
                        RelatedVehicleIssueId = s.TripVehicleHandoverIssueId,
                        RelatedDeliveryIssueId = s.TripDeliveryIssueId
                    })
                    .ToListAsync();

                return new ResponseDTO("Lấy danh sách thành công.", 200, true, surcharges);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi lấy danh sách: {ex.Message}", 500, false);
            }
        }

        // ========================================================================
        // 3. CẬP NHẬT TRẠNG THÁI (Thanh toán / Hủy)
        // ========================================================================
        public async Task<ResponseDTO> UpdateStatusAsync(UpdateSurchargeStatusDTO dto)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                // Check role nếu cần (vd: Chỉ Owner mới được Cancel, chỉ Hệ thống thanh toán mới được set PAID)

                var surcharge = await _unitOfWork.TripSurchargeRepo.GetByIdAsync(dto.TripSurchargeId);
                if (surcharge == null) return new ResponseDTO("Khoản phụ phí không tồn tại.", 404, false);

                surcharge.Status = dto.NewStatus;

                // Nếu trạng thái là PAID -> Cập nhật thời gian trả
                if (dto.NewStatus == SurchargeStatus.PAID)
                {
                    surcharge.PaidAt = DateTime.UtcNow;
                }
                else
                {
                    surcharge.PaidAt = null;
                }

                await _unitOfWork.TripSurchargeRepo.UpdateAsync(surcharge);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Cập nhật trạng thái thành công.", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi cập nhật: {ex.Message}", 500, false);
            }
        }
    }
}