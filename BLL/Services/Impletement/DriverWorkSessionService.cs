using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
using Common.Helpers;
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

        // =========================================================================
        // [MỚI] 6. IMPORT HISTORY (NHẬP LỊCH SỬ CHẠY THEO NGÀY)
        // =========================================================================
        public async Task<ResponseDTO> ImportDriverHistoryAsync(ImportDriverHistoryDTO dto)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var driverId = _userUtility.GetUserIdFromToken();
                if (driverId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                // 1. Xác định giới hạn tuần hiện tại (Để chỉ cho phép nhập trong tuần này)
                var now = DateTime.UtcNow;
                int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
                var startOfWeek = now.Date.AddDays(-1 * diff); // 00:00 Thứ 2
                var endOfWeek = startOfWeek.AddDays(7).AddTicks(-1); // 23:59 Chủ nhật

                // 2. Validate dữ liệu đầu vào
                foreach (var log in dto.DailyLogs)
                {
                    // Chỉ cho nhập dữ liệu trong tuần này và không được nhập tương lai
                    if (log.Date.Date < startOfWeek || log.Date.Date > now.Date)
                    {
                        return new ResponseDTO($"Ngày {log.Date:dd/MM} không thuộc tuần hiện tại hoặc ở tương lai.", 400, false);
                    }
                    if (log.HoursDriven < 0 || log.HoursDriven > 24)
                    {
                        return new ResponseDTO($"Số giờ nhập cho ngày {log.Date:dd/MM} không hợp lệ (0-24h).", 400, false);
                    }
                }

                // 3. Xóa dữ liệu lịch sử CŨ trong tuần này (Cơ chế ghi đè - Reset history cũ)
                // Lưu ý: Chỉ xóa những record có TripId == null (tức là record nhập tay), không xóa chuyến đi thật
                var oldHistorySessions = await _unitOfWork.DriverWorkSessionRepo.GetAll()
                    .Where(s => s.DriverId == driverId
                                && s.TripId == null // Chỉ xóa session nhập tay
                                && s.StartTime >= startOfWeek
                                && s.StartTime <= endOfWeek)
                    .ToListAsync();

                if (oldHistorySessions.Any())
                {
                    // Bạn cần hàm DeleteRange trong Repository, hoặc loop delete
                    _unitOfWork.DriverWorkSessionRepo.DeleteRange(oldHistorySessions);
                }

                // 4. Tạo Session mới từ dữ liệu nhập
                foreach (var log in dto.DailyLogs)
                {
                    if (log.HoursDriven <= 0) continue;

                    // QUY ƯỚC KHOA HỌC:
                    // Luôn giả định tài xế bắt đầu chạy từ 08:00 sáng.
                    // Điều này giúp tránh trùng lặp ngẫu nhiên và dễ tính toán.
                    var sessionDate = log.Date.Date; // Lấy phần ngày, giờ là 00:00
                    var simulatedStartTime = sessionDate.AddHours(8); // 08:00 AM
                    var simulatedEndTime = simulatedStartTime.AddHours(log.HoursDriven);

                    // Kiểm tra: Nếu là "Hôm nay", và giờ kết thúc vượt quá giờ hiện tại -> Điều chỉnh lại
                    // Ví dụ: Bây giờ là 10h sáng, mà nhập đã lái 8 tiếng (lẽ ra phải xong lúc 16h) -> Vô lý
                    // -> Lùi thời gian lại: Kết thúc lúc Now, Bắt đầu lúc Now - HoursDriven
                    if (sessionDate == now.Date && simulatedEndTime > now)
                    {
                        simulatedEndTime = now;
                        simulatedStartTime = now.AddHours(-log.HoursDriven);
                    }

                    // Trong vòng lặp foreach (var log in dto.DailyLogs)
                    var session = new DriverWorkSession
                    {
                        DriverWorkSessionId = Guid.NewGuid(),
                        DriverId = driverId,
                        TripId = null, // TripId là null
                        CreatedAt = DateTime.UtcNow,
                    };

                    // Gọi hàm helper của Entity để set thời gian và tự tính DurationInHours
                    session.SetHistoryData(simulatedStartTime, simulatedEndTime);

                    await _unitOfWork.DriverWorkSessionRepo.AddAsync(session);
                }

                // --- ĐOẠN MỚI: CẬP NHẬT TRẠNG THÁI DRIVER ---
                var driver = await _unitOfWork.DriverRepo.GetByIdAsync(driverId);
                if (driver != null && !driver.HasDeclaredInitialHistory)
                {
                    driver.HasDeclaredInitialHistory = true;
                    await _unitOfWork.DriverRepo.UpdateAsync(driver);
                }
                // --------------------------------------------

                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                // 5. Tính toán lại xem sau khi nhập xong thì còn được lái bao nhiêu
                var eligibility = await CalculateDriverHoursAsync(driverId);

                return new ResponseDTO($"Cập nhật lịch sử thành công. Hôm nay bạn đã lái {eligibility.HoursDrivenToday:F1}h.", 200, true);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO("Lỗi: " + ex.Message, 500, false);
            }
        }

        public async Task<DriverAvailabilityDTO> CheckDriverAvailabilityAsync(Guid driverId)
        {
            return await CalculateDriverHoursForAssignAsync(driverId);
        }

        private async Task<DriverAvailabilityDTO> CalculateDriverHoursForAssignAsync(Guid driverId)
        {
            // 1. Dùng thống nhất múi giờ (UTC)
            var now = DateTime.UtcNow;

            // Xác định khung giờ Ngày
            var startOfDay = now.Date;
            var endOfDay = startOfDay.AddDays(1);

            // Xác định khung giờ Tuần (Bắt đầu từ Thứ 2)
            int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
            var startOfWeek = startOfDay.AddDays(-1 * diff);
            var endOfWeek = startOfWeek.AddDays(7);

            // 2. Query tối ưu
            var query = _unitOfWork.DriverWorkSessionRepo.GetAll();

            var sessions = await query
                .AsNoTracking() // Thêm AsNoTracking cho nhẹ
                .Where(s => s.DriverId == driverId
                            && s.StartTime < endOfWeek
                            && (s.EndTime == null || s.EndTime > startOfWeek))
                .ToListAsync();

            double hoursToday = 0;
            double hoursWeek = 0;

            // 3. Tính toán trong bộ nhớ
            foreach (var s in sessions)
            {
                var actualEndTime = s.EndTime ?? now;

                // --- TÍNH GIỜ TRONG NGÀY ---
                var overlapStartDay = s.StartTime > startOfDay ? s.StartTime : startOfDay;
                var overlapEndDay = actualEndTime < endOfDay ? actualEndTime : endOfDay;

                if (overlapEndDay > overlapStartDay)
                {
                    hoursToday += (overlapEndDay - overlapStartDay).TotalHours;
                }

                // --- TÍNH GIỜ TRONG TUẦN ---
                var overlapStartWeek = s.StartTime > startOfWeek ? s.StartTime : startOfWeek;
                var overlapEndWeek = actualEndTime < endOfWeek ? actualEndTime : endOfWeek;

                if (overlapEndWeek > overlapStartWeek)
                {
                    hoursWeek += (overlapEndWeek - overlapStartWeek).TotalHours;
                }
            }

            // [MỚI] TÍNH SỐ GIỜ CÒN LẠI (QUOTA)
            double weeklyLimit = 48.0;
            double remainingWeek = weeklyLimit - hoursWeek;
            if (remainingWeek < 0) remainingWeek = 0;

            // 4. Kiểm tra giới hạn và Trả về kết quả

            // Case 1: Quá giờ ngày (10h)
            if (hoursToday >= 10)
            {
                return new DriverAvailabilityDTO
                {
                    CanDrive = false,
                    Message = $"Đã lái quá giới hạn ngày ({hoursToday:F1}/10h).",
                    HoursDrivenToday = hoursToday,
                    HoursDrivenThisWeek = hoursWeek,
                    RemainingHoursThisWeek = remainingWeek // Vẫn trả về để biết quota tuần
                };
            }

            // Case 2: Quá giờ tuần (48h)
            if (hoursWeek >= weeklyLimit)
            {
                return new DriverAvailabilityDTO
                {
                    CanDrive = false,
                    Message = $"Đã lái quá giới hạn tuần ({hoursWeek:F1}/48h).",
                    HoursDrivenToday = hoursToday,
                    HoursDrivenThisWeek = hoursWeek,
                    RemainingHoursThisWeek = 0 // Hết giờ
                };
            }

            // Case 3: OK
            return new DriverAvailabilityDTO
            {
                CanDrive = true,
                Message = "Tài xế khả dụng.",
                HoursDrivenToday = hoursToday,
                HoursDrivenThisWeek = hoursWeek,
                RemainingHoursThisWeek = remainingWeek // Trả về số giờ còn lại
            };
        }




        // =========================================================================
        // 1. PUBLIC API: LẤY THỜI GIAN AVAILABLE (Dùng cho App Tài xế xem Dashboard)
        // =========================================================================
        public async Task<ResponseDTO> GetDriverCurrentAvailabilityAsync()
        {
            try
            {
                var driverId = _userUtility.GetUserIdFromToken();
                if (driverId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                // Gọi hàm tính toán nội bộ
                var result = await CalculateDriverAvailabilityInternalAsync(driverId);

                return new ResponseDTO("Success", 200, true, result);
            }
            catch (Exception ex)
            {
                return new ResponseDTO(ex.Message, 500, false);
            }
        }

        // =========================================================================
        // 2. PUBLIC API: VALIDATE TÀI XẾ VỚI 1 TRIP CỤ THỂ (Dùng khi xem chi tiết Trip)
        // =========================================================================
        public async Task<ResponseDTO> ValidateDriverForTripAsync(Guid tripId)
        {
            try
            {
                var driverId = _userUtility.GetUserIdFromToken();
                if (driverId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                // Gọi hàm check nội bộ
                var checkResult = await CheckDriverCapabilityInternalAsync(driverId, tripId);

                if (!checkResult.IsSuitable)
                {
                    // Trả về 200 nhưng data báo false để Frontend hiện lý do (hoặc 400 tùy quy định)
                    return new ResponseDTO(checkResult.Reason, 400, false, checkResult);
                }

                return new ResponseDTO("Tài xế phù hợp với chuyến đi.", 200, true, checkResult);
            }
            catch (Exception ex)
            {
                return new ResponseDTO(ex.Message, 500, false);
            }
        }

        // =========================================================================
        // 3. INTERNAL CHECK: DÙNG CHO CẢ VALIDATE VÀ LÚC TẠO ASSIGNMENT
        // =========================================================================
        public async Task<DriverSuitabilityDTO> CheckDriverCapabilityInternalAsync(Guid driverId, Guid tripId)
        {
            // A. Lấy thông tin Trip & Route
            var trip = await _unitOfWork.TripRepo.GetAll()
                .Include(t => t.ShippingRoute)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TripId == tripId);

            if (trip == null) return new DriverSuitabilityDTO { IsSuitable = false, Reason = "Trip not found." };

            // B. Tính toán nhu cầu chuyến đi

            // Ưu tiên lấy Estimated từ ShippingRoute
            double distance = trip.ShippingRoute.EstimatedDistanceKm > 0
                ? trip.ShippingRoute.EstimatedDistanceKm
                : (double)trip.ActualDistanceKm;

            double tripDuration = trip.ShippingRoute.EstimatedDurationHours > 0
                ? trip.ShippingRoute.EstimatedDurationHours
                : trip.ActualDuration.TotalHours;

            // Fallback
            if (tripDuration <= 0 && distance > 0) tripDuration = distance / 50.0;

            // [FIX LỖI BIÊN DỊCH TẠI ĐÂY]
            // Lấy thêm WaitTime và BufferTime để truyền đủ 6 tham số
            double waitTimeHours = trip.ShippingRoute.WaitTimeHours ?? 0;
            double bufferHours = tripDuration * 0.15; // Tạm tính 15%

            // Gọi Helper với đủ 6 tham số
            var suggestion = TripCalculationHelper.CalculateScenarios(
                distance,
                tripDuration,
                waitTimeHours, // <--- Tham số 3
                bufferHours,   // <--- Tham số 4
                trip.ShippingRoute.ExpectedPickupDate,
                trip.ShippingRoute.ExpectedDeliveryDate
            );

            // Xác định số giờ YÊU CẦU
            double requiredHours = 0;

            // [LOGIC MỚI] Check xem Owner đã chốt Mode chưa (như logic ta bàn ở câu trước)
            // Nếu chưa chốt, dùng System Recommendation
            // Ở đây giả sử dùng Recommendation cho đơn giản (hoặc bạn có thể check trip.ExecutionMode nếu đã thêm)
            string mode = suggestion.SystemRecommendation;

            if (mode == "SOLO") requiredHours = suggestion.SoloScenario.DrivingHoursPerDriver;
            else if (mode == "TEAM") requiredHours = suggestion.TeamScenario.DrivingHoursPerDriver;
            else requiredHours = suggestion.TeamScenario.DrivingHoursPerDriver; // Fallback TEAM

            // C. Lấy Quota hiện tại
            var availability = await CalculateDriverAvailabilityInternalAsync(driverId);

            // D. CHECK 1: Quỹ thời gian
            if (availability.IsBanned)
            {
                return new DriverSuitabilityDTO
                {
                    IsSuitable = false,
                    Reason = "Tài xế đang bị cấm lái do vượt quá giới hạn tuần.",
                    RequiredHours = requiredHours,
                    DriverRemainingHours = availability.RemainingHoursThisWeek
                };
            }

            if (availability.RemainingHoursThisWeek < requiredHours)
            {
                return new DriverSuitabilityDTO
                {
                    IsSuitable = false,
                    Reason = $"Thiếu giờ lái. Cần {requiredHours:F1}h, chỉ còn {availability.RemainingHoursThisWeek:F1}h.",
                    RequiredHours = requiredHours,
                    DriverRemainingHours = availability.RemainingHoursThisWeek
                };
            }

            // E. CHECK 2: Trùng lịch (Schedule Conflict)
            var startTrip = trip.ShippingRoute.ExpectedPickupDate;
            var endTrip = trip.ShippingRoute.ExpectedDeliveryDate;

            bool isConflict = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
                .Include(a => a.Trip).ThenInclude(t => t.ShippingRoute)
                .AsNoTracking()
                .AnyAsync(a =>
                    a.DriverId == driverId &&
                    (a.AssignmentStatus == AssignmentStatus.ACCEPTED) && // Đã sửa ACCEPTED lặp lại thành ASSIGNED
                    a.TripId != tripId &&
                    a.Trip.ShippingRoute.ExpectedPickupDate < endTrip &&
                    a.Trip.ShippingRoute.ExpectedDeliveryDate > startTrip
                );

            if (isConflict)
            {
                return new DriverSuitabilityDTO
                {
                    IsSuitable = false,
                    Reason = "Tài xế bị trùng lịch với chuyến đi khác.",
                    RequiredHours = requiredHours,
                    DriverRemainingHours = availability.RemainingHoursThisWeek
                };
            }

            // F. Success
            return new DriverSuitabilityDTO
            {
                IsSuitable = true,
                Reason = "Success",
                RequiredHours = requiredHours,
                DriverRemainingHours = availability.RemainingHoursThisWeek
            };
        }

        // =========================================================================
        // 4. PRIVATE HELPER: TÍNH TOÁN GIỜ LÁI (CORE LOGIC)
        // =========================================================================
        // Hàm này tách ra để cả API Public và Internal Check đều gọi được
        private async Task<DriverAvailabilityInPostTripDTO> CalculateDriverAvailabilityInternalAsync(Guid driverId)
        {
            var now = DateTime.UtcNow;
            var startOfDay = now.Date;
            var endOfDay = startOfDay.AddDays(1);

            // Tính đầu tuần (Thứ 2)
            int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
            var startOfWeek = startOfDay.AddDays(-1 * diff);
            var endOfWeek = startOfWeek.AddDays(7);

            var sessions = await _unitOfWork.DriverWorkSessionRepo.GetAll()
                .AsNoTracking()
                .Where(s => s.DriverId == driverId
                            && s.StartTime < endOfWeek
                            && (s.EndTime == null || s.EndTime > startOfWeek))
                .ToListAsync();

            double hoursToday = 0;
            double hoursWeek = 0;

            foreach (var s in sessions)
            {
                var sEnd = s.EndTime ?? now;

                // Tính giao nhau trong Ngày
                var overlapStartDay = s.StartTime > startOfDay ? s.StartTime : startOfDay;
                var overlapEndDay = sEnd < endOfDay ? sEnd : endOfDay;
                if (overlapEndDay > overlapStartDay)
                    hoursToday += (overlapEndDay - overlapStartDay).TotalHours;

                // Tính giao nhau trong Tuần
                var overlapStartWeek = s.StartTime > startOfWeek ? s.StartTime : startOfWeek;
                var overlapEndWeek = sEnd < endOfWeek ? sEnd : endOfWeek;
                if (overlapEndWeek > overlapStartWeek)
                    hoursWeek += (overlapEndWeek - overlapStartWeek).TotalHours;
            }

            return new DriverAvailabilityInPostTripDTO
            {
                DriverId = driverId,
                DrivenHoursToday = Math.Round(hoursToday, 1),
                RemainingHoursToday = Math.Max(0, 10.0 - hoursToday),
                DrivenHoursThisWeek = Math.Round(hoursWeek, 1),
                RemainingHoursThisWeek = Math.Max(0, 48.0 - hoursWeek),
                IsBanned = (hoursToday > 10 || hoursWeek > 48),
                Message = (hoursToday > 10 || hoursWeek > 48) ? "Đã vượt quá giới hạn." : "Đủ điều kiện."
            };
        }
    }
}