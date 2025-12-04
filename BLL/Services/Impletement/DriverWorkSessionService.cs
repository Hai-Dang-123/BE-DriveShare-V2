using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
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
    public class DriverWorkSessionService : IDriverWorkSessionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;

        public DriverWorkSessionService(IUnitOfWork unitOfWork, UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
        }

        // =========================================================================
        // [MỚI] 5. GET CURRENT SESSION IN TRIP (LẤY SESSION ĐANG CHẠY CỦA TRIP)
        // =========================================================================
        public async Task<ResponseDTO> GetCurrentSessionInTripAsync(Guid tripId)
        {
            try
            {
                var currentUserId = _userUtility.GetUserIdFromToken(); // Lấy ID người đang xem

                // 1. Tìm Session đang IN_PROGRESS của Trip này
                var activeSession = await _unitOfWork.DriverWorkSessionRepo.GetAll()
                    .AsNoTracking()
                    .Include(s => s.Driver)
                    .FirstOrDefaultAsync(s => s.TripId == tripId && s.Status == WorkSessionStatus.IN_PROGRESS);

                if (activeSession == null)
                {
                    // Không có ai đang lái -> Trả về null hoặc object rỗng tùy quy ước FE
                    return new ResponseDTO("Hiện tại không có ai đang lái chuyến này.", 200, true, null);
                }

                // 2. Lấy Role của tài xế đang lái (Để hiển thị Tài chính/Tài phụ)
                var driverRoleType = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
                    .AsNoTracking()
                    .Where(a => a.TripId == tripId && a.DriverId == activeSession.DriverId)
                    .Select(a => a.Type)
                    .FirstOrDefaultAsync();

                string roleName = driverRoleType == DriverType.PRIMARY ? "Tài Chính" : "Tài Phụ";

                // 3. Map sang DTO
                var result = new CurrentSessionDTO
                {
                    SessionId = activeSession.DriverWorkSessionId,
                    DriverId = activeSession.DriverId,
                    DriverName = activeSession.Driver?.FullName ?? "N/A",
                    DriverPhone = activeSession.Driver?.PhoneNumber ?? "N/A",
                    Role = roleName,
                    StartTime = activeSession.StartTime,
                    IsSelf = (activeSession.DriverId == currentUserId) // Đánh dấu nếu là chính mình
                };

                return new ResponseDTO("Lấy thông tin session hiện tại thành công.", 200, true, result);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi: {ex.Message}", 500, false);
            }
        }

        // =========================================================================
        // 1. START SESSION (BẮT ĐẦU CHẠY XE)
        // =========================================================================
        public async Task<ResponseDTO> StartSessionAsync(StartSessionDTO dto)
        {
            try
            {
                var currentDriverId = _userUtility.GetUserIdFromToken();
                if (currentDriverId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                // 1. Kiểm tra quyền của TÔI
                var myAssignment = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
                    .FirstOrDefaultAsync(a => a.TripId == dto.TripId && a.DriverId == currentDriverId && a.AssignmentStatus == AssignmentStatus.ACCEPTED);

                if (myAssignment == null) return new ResponseDTO("Bạn không được phân công chạy chuyến này.", 403, false);

                // 2. Kiểm tra chính tôi có đang kẹt chuyến khác không
                var myActiveSession = await _unitOfWork.DriverWorkSessionRepo.GetAll()
                    .FirstOrDefaultAsync(x => x.DriverId == currentDriverId && x.Status == WorkSessionStatus.IN_PROGRESS);

                if (myActiveSession != null) return new ResponseDTO($"Bạn đang chạy dở chuyến khác (ID: {myActiveSession.TripId}).", 400, false);

                // 3. KIỂM TRA XUNG ĐỘT (CÓ AI KHÁC ĐANG LÁI KHÔNG?)
                var runningSession = await _unitOfWork.DriverWorkSessionRepo.GetAll()
                    .Include(s => s.Driver)
                    .Include(s => s.Trip).ThenInclude(t => t.Vehicle)
                    .FirstOrDefaultAsync(x => x.TripId == dto.TripId && x.Status == WorkSessionStatus.IN_PROGRESS);

                if (runningSession != null)
                {
                    var runningDriverType = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
                        .Where(a => a.TripId == dto.TripId && a.DriverId == runningSession.DriverId)
                        .Select(a => a.Type)
                        .FirstOrDefaultAsync();

                    string roleName = runningDriverType == DriverType.PRIMARY ? "Tài Chính" : "Tài Phụ";
                    string driverName = runningSession.Driver?.FullName ?? "Ẩn danh";
                    string driverPhone = runningSession.Driver?.PhoneNumber ?? "N/A";
                    string plateNumber = runningSession.Trip?.Vehicle?.PlateNumber ?? "N/A";

                    var conflictDto = new DriverConflictDTO
                    {
                        ConflictDriverId = runningSession.DriverId,
                        ConflictDriverName = driverName,
                        ConflictDriverPhone = driverPhone,
                        ConflictDriverRole = roleName,
                        SessionStartTime = runningSession.StartTime,
                        LicensePlate = plateNumber
                    };

                    return new ResponseDTO(
                        $"KHÔNG THỂ BẮT ĐẦU: Xe {plateNumber} đang được lái bởi {roleName} - {driverName}.",
                        409,
                        false,
                        conflictDto
                    );
                }

                // 4. Kiểm tra luật 10h/48h
                var eligibility = await CalculateDriverHoursAsync(currentDriverId);
                if (!eligibility.CanDrive) return new ResponseDTO($"Không thể nhận chuyến: {eligibility.Message}", 403, false);

                // 5. Tạo phiên làm việc
                var newSession = new DriverWorkSession
                {
                    DriverWorkSessionId = Guid.NewGuid(),
                    DriverId = currentDriverId,
                    TripId = dto.TripId,
                    StartTime = DateTime.UtcNow,
                    Status = WorkSessionStatus.IN_PROGRESS,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.DriverWorkSessionRepo.AddAsync(newSession);
                await _unitOfWork.SaveChangeAsync();

                var successDto = new StartSessionSuccessDTO
                {
                    SessionId = newSession.DriverWorkSessionId,
                    Role = myAssignment.Type.ToString(),
                    Message = "Chúc bạn thượng lộ bình an!"
                };

                string myRoleName = myAssignment.Type == DriverType.PRIMARY ? "Tài Chính" : "Tài Phụ";
                return new ResponseDTO($"Bắt đầu lái xe thành công ({myRoleName}).", 200, true, successDto);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Lỗi hệ thống: " + ex.Message, 500, false);
            }
        }

        // =========================================================================
        // 2. END SESSION (KẾT THÚC CHẠY XE)
        // =========================================================================
        public async Task<ResponseDTO> EndSessionAsync(EndSessionDTO dto)
        {
            try
            {
                var currentDriverId = _userUtility.GetUserIdFromToken();
                var session = await _unitOfWork.DriverWorkSessionRepo.GetByIdAsync(dto.DriverWorkSessionId);

                if (session == null) return new ResponseDTO("Không tìm thấy phiên làm việc.", 404, false);
                if (session.DriverId != currentDriverId) return new ResponseDTO("Forbidden.", 403, false);
                if (session.Status == WorkSessionStatus.COMPLETED) return new ResponseDTO("Phiên đã kết thúc.", 400, false);

                session.CompleteSession();

                await _unitOfWork.DriverWorkSessionRepo.UpdateAsync(session);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Kết thúc lái xe thành công.", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Lỗi hệ thống: " + ex.Message, 500, false);
            }
        }

        // =========================================================================
        // 3. CHECK ELIGIBILITY
        // =========================================================================
        public async Task<ResponseDTO> CheckDriverEligibilityAsync()
        {
            try
            {
                var currentDriverId = _userUtility.GetUserIdFromToken();
                var data = await CalculateDriverHoursAsync(currentDriverId);
                return new ResponseDTO("Success", 200, true, data);
            }
            catch (Exception ex) { return new ResponseDTO(ex.Message, 500, false); }
        }

        // =========================================================================
        // 4. GET HISTORY
        // =========================================================================
        public async Task<ResponseDTO> GetDriverHistoryAsync(DriverHistoryFilterDTO filter)
        {
            try
            {
                var driverId = _userUtility.GetUserIdFromToken();
                var query = _unitOfWork.DriverWorkSessionRepo.GetAll().AsNoTracking().Where(x => x.DriverId == driverId);

                if (filter.FromDate.HasValue) query = query.Where(x => x.StartTime >= filter.FromDate.Value.Date);
                if (filter.ToDate.HasValue) query = query.Where(x => x.StartTime <= filter.ToDate.Value.Date.AddDays(1).AddTicks(-1));

                query = query.OrderByDescending(x => x.StartTime);

                var totalHours = await query.Where(x => x.EndTime != null).SumAsync(x => x.DurationInHours);
                var totalRecords = await query.CountAsync();

                var dataList = await query
                    .Skip((filter.PageIndex - 1) * filter.PageSize)
                    .Take(filter.PageSize)
                    .Select(x => new DriverSessionHistoryDTO
                    {
                        SessionId = x.DriverWorkSessionId,
                        TripId = x.TripId,
                        StartTime = x.StartTime,
                        EndTime = x.EndTime,
                        Status = x.Status.ToString(),
                        DurationHours = x.EndTime.HasValue ? x.DurationInHours : (DateTime.UtcNow - x.StartTime).TotalHours
                    })
                    .ToListAsync();

                var result = new HistoryResponseDTO { TotalHoursInPeriod = totalHours, TotalSessions = totalRecords, Sessions = dataList };
                return new ResponseDTO("Success", 200, true, result);
            }
            catch (Exception ex) { return new ResponseDTO(ex.Message, 500, false); }
        }

        // =========================================================================
        // PRIVATE HELPER
        // =========================================================================
        private async Task<DriverAvailabilityDTO> CalculateDriverHoursAsync(Guid driverId)
        {
            var now = DateTime.UtcNow;
            var startOfDay = now.Date;
            var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

            int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
            var startOfWeek = startOfDay.AddDays(-1 * diff);
            var endOfWeek = startOfWeek.AddDays(7).AddTicks(-1);

            var sessions = await _unitOfWork.DriverWorkSessionRepo.GetAll()
                .Where(s => s.DriverId == driverId && s.StartTime < endOfWeek && (s.EndTime == null || s.EndTime > startOfWeek))
                .ToListAsync();

            double hoursToday = 0;
            double hoursWeek = 0;

            foreach (var s in sessions)
            {
                var sEnd = s.EndTime ?? now;
                var overlapStartDay = s.StartTime > startOfDay ? s.StartTime : startOfDay;
                var overlapEndDay = sEnd < endOfDay ? sEnd : endOfDay;
                if (overlapEndDay > overlapStartDay) hoursToday += (overlapEndDay - overlapStartDay).TotalHours;

                var overlapStartWeek = s.StartTime > startOfWeek ? s.StartTime : startOfWeek;
                var overlapEndWeek = sEnd < endOfWeek ? sEnd : endOfWeek;
                if (overlapEndWeek > overlapStartWeek) hoursWeek += (overlapEndWeek - overlapStartWeek).TotalHours;
            }

            if (hoursToday >= 10) return new DriverAvailabilityDTO { CanDrive = false, Message = $"Over daily limit ({hoursToday:F1}/10h)", HoursDrivenToday = hoursToday, HoursDrivenThisWeek = hoursWeek };
            if (hoursWeek >= 48) return new DriverAvailabilityDTO { CanDrive = false, Message = $"Over weekly limit ({hoursWeek:F1}/48h)", HoursDrivenToday = hoursToday, HoursDrivenThisWeek = hoursWeek };

            return new DriverAvailabilityDTO { CanDrive = true, Message = "OK", HoursDrivenToday = hoursToday, HoursDrivenThisWeek = hoursWeek };
        }
    }
}