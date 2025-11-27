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
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class DriverWorkSessionService : IDriverWorkSessionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility; // Inject UserUtility

        public DriverWorkSessionService(IUnitOfWork unitOfWork, UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
        }

        public async Task<ResponseDTO> StartSessionAsync(StartSessionDTO dto)
        {
            try
            {
                // 1. Lấy ID từ Token (Bảo mật hơn, không tin tưởng dữ liệu client gửi)
                var currentDriverId = _userUtility.GetUserIdFromToken();

                // 2. Kiểm tra xem tài xế có đang chạy chuyến nào dở dang không
                var activeSession = await _unitOfWork.DriverWorkSessionRepo.GetAll()
                    .FirstOrDefaultAsync(x => x.DriverId == currentDriverId && x.Status == WorkSessionStatus.IN_PROGRESS);

                if (activeSession != null)
                {
                    return new ResponseDTO("Bạn đang trong một chuyến đi chưa kết thúc.", 400, false);
                }

                // 3. KIỂM TRA LUẬT 10H/NGÀY VÀ 48H/TUẦN
                var eligibility = await CalculateDriverHoursAsync(currentDriverId);

                if (!eligibility.CanDrive)
                {
                    return new ResponseDTO($"Không thể nhận chuyến: {eligibility.Message}", 403, false);
                }

                // 4. Tạo phiên làm việc mới
                var newSession = new DriverWorkSession
                {
                    DriverWorkSessionId = Guid.NewGuid(),
                    DriverId = currentDriverId, // Dùng ID từ token
                    TripId = dto.TripId,
                    StartTime = DateTime.UtcNow,
                    Status = WorkSessionStatus.IN_PROGRESS,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.DriverWorkSessionRepo.AddAsync(newSession);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Bắt đầu chuyến đi thành công.", 200, true, newSession.DriverWorkSessionId);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Lỗi hệ thống: " + ex.Message, 500, false);
            }
        }

        public async Task<ResponseDTO> EndSessionAsync(EndSessionDTO dto)
        {
            try
            {
                var currentDriverId = _userUtility.GetUserIdFromToken();
                var session = await _unitOfWork.DriverWorkSessionRepo.GetByIdAsync(dto.DriverWorkSessionId);

                // 1. Kiểm tra tồn tại
                if (session == null)
                {
                    return new ResponseDTO("Không tìm thấy phiên làm việc.", 404, false);
                }

                // 2. BẢO MẬT: Kiểm tra xem session này có đúng là của tài xế đang đăng nhập không
                if (session.DriverId != currentDriverId)
                {
                    return new ResponseDTO("Bạn không có quyền kết thúc phiên làm việc của người khác.", 403, false);
                }

                // 3. Kiểm tra trạng thái
                if (session.Status == WorkSessionStatus.COMPLETED)
                {
                    return new ResponseDTO("Phiên làm việc này đã kết thúc trước đó rồi.", 400, false);
                }

                // 4. Chốt sổ (Sử dụng hàm trong Entity hoặc set thủ công)
                session.CompleteSession();
                // Hoặc:
                //session.EndTime = DateTime.UtcNow;
                //session.Status = WorkSessionStatus.COMPLETED;
                //session.DurationInHours = (session.EndTime.Value - session.StartTime).TotalHours;

                await _unitOfWork.DriverWorkSessionRepo.UpdateAsync(session);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Kết thúc chuyến đi thành công.", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Lỗi hệ thống khi kết thúc chuyến đi.", 500, false);
            }
        }

        public async Task<ResponseDTO> CheckDriverEligibilityAsync()
        {
            try
            {
                var currentDriverId = _userUtility.GetUserIdFromToken();
                var data = await CalculateDriverHoursAsync(currentDriverId);
                return new ResponseDTO("Lấy thông tin thành công", 200, true, data);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Lỗi kiểm tra thông tin.", 500, false);
            }
        }

        // =================================================================
        // Private Helper: Giữ nguyên logic tính toán (Cross-Midnight)
        // =================================================================
        private async Task<DriverAvailabilityDTO> CalculateDriverHoursAsync(Guid driverId)
        {
            var now = DateTime.UtcNow;
            var startOfDay = now.Date;
            var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

            // Tính đầu tuần (Thứ 2)
            int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
            var startOfWeek = startOfDay.AddDays(-1 * diff);
            var endOfWeek = startOfWeek.AddDays(7).AddTicks(-1);

            // Query dữ liệu
            var sessions = await _unitOfWork.DriverWorkSessionRepo.GetAll()
                .Where(s => s.DriverId == driverId
                            && s.StartTime < endOfWeek
                            && (s.EndTime == null || s.EndTime > startOfWeek))
                .ToListAsync();

            double hoursToday = 0;
            double hoursWeek = 0;

            foreach (var s in sessions)
            {
                var sEnd = s.EndTime ?? now;

                // Tính giờ Hôm nay
                var overlapStartDay = s.StartTime > startOfDay ? s.StartTime : startOfDay;
                var overlapEndDay = sEnd < endOfDay ? sEnd : endOfDay;
                if (overlapEndDay > overlapStartDay) hoursToday += (overlapEndDay - overlapStartDay).TotalHours;

                // Tính giờ Tuần này
                var overlapStartWeek = s.StartTime > startOfWeek ? s.StartTime : startOfWeek;
                var overlapEndWeek = sEnd < endOfWeek ? sEnd : endOfWeek;
                if (overlapEndWeek > overlapStartWeek) hoursWeek += (overlapEndWeek - overlapStartWeek).TotalHours;
            }

            if (hoursToday >= 10)
                return new DriverAvailabilityDTO { CanDrive = false, Message = $"Đã lái {hoursToday:F1}/10h hôm nay.", HoursDrivenToday = hoursToday, HoursDrivenThisWeek = hoursWeek };

            if (hoursWeek >= 48)
                return new DriverAvailabilityDTO { CanDrive = false, Message = $"Đã lái {hoursWeek:F1}/48h tuần này.", HoursDrivenToday = hoursToday, HoursDrivenThisWeek = hoursWeek };

            return new DriverAvailabilityDTO { CanDrive = true, Message = "Đủ điều kiện.", HoursDrivenToday = hoursToday, HoursDrivenThisWeek = hoursWeek };
        }

        public async Task<ResponseDTO> GetDriverHistoryAsync(DriverHistoryFilterDTO filter)
        {
            try
            {
                // 1. Lấy ID tài xế từ Token
                var driverId = _userUtility.GetUserIdFromToken();

                // 2. Khởi tạo Query
                // Sử dụng AsNoTracking() để tối ưu hiệu năng vì chỉ đọc dữ liệu (Read-Only)
                var query = _unitOfWork.DriverWorkSessionRepo.GetAll()
                    .AsNoTracking()
                    .Where(x => x.DriverId == driverId);

                // 3. Áp dụng bộ lọc ngày tháng (Nếu người dùng truyền vào)
                if (filter.FromDate.HasValue)
                {
                    // Lấy từ đầu ngày của FromDate
                    var from = filter.FromDate.Value.Date;
                    query = query.Where(x => x.StartTime >= from);
                }

                if (filter.ToDate.HasValue)
                {
                    // Lấy đến cuối ngày của ToDate (23:59:59)
                    var to = filter.ToDate.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(x => x.StartTime <= to);
                }

                // 4. Sắp xếp: Mới nhất lên đầu
                query = query.OrderByDescending(x => x.StartTime);

                // 5. Tính toán số liệu tổng hợp (Trước khi phân trang)
                // Lưu ý: Chỉ tính tổng giờ của các chuyến ĐÃ HOÀN THÀNH (có DurationInHours)
                var totalHours = await query
                    .Where(x => x.EndTime != null)
                    .SumAsync(x => x.DurationInHours);

                var totalRecords = await query.CountAsync();

                // 6. Phân trang (Paging) và Lấy dữ liệu
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
                        // Nếu chuyến đang chạy (EndTime null), tính thời gian tạm tính tới hiện tại
                        DurationHours = x.EndTime.HasValue
                                        ? x.DurationInHours
                                        : (DateTime.UtcNow - x.StartTime).TotalHours
                    })
                    .ToListAsync();

                // 7. Đóng gói dữ liệu trả về
                var result = new HistoryResponseDTO
                {
                    TotalHoursInPeriod = totalHours,
                    TotalSessions = totalRecords,
                    Sessions = dataList
                };

                return new ResponseDTO("Lấy lịch sử thành công", 200, true, result);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Lỗi khi lấy lịch sử: " + ex.Message, 500, false);
            }
        }
    }
}
