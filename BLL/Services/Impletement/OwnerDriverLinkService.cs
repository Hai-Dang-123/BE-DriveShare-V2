using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status; // Đảm bảo using Enum này
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
    public class OwnerDriverLinkService : IOwnerDriverLinkService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;

        public OwnerDriverLinkService(IUnitOfWork unitOfWork, UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
        }

        public async Task<ResponseDTO> ChangeStatusAsync(ChangeStatusOwnerDriverLinkDTO dto)
        {
            var currentUserId = _userUtility.GetUserIdFromToken();
            if (currentUserId == Guid.Empty)
            {
                return new ResponseDTO("User is not authenticated.", 401, false);
            }

            var link = await _unitOfWork.OwnerDriverLinkRepo.GetByIdAsync(dto.OwnerDriverLinkId);
            if (link == null)
            {
                // Nên dùng 404 Not Found
                return new ResponseDTO($"Link with ID {dto.OwnerDriverLinkId} not found.", 404, false);
            }


            if (currentUserId != link.OwnerId)
            {
                return new ResponseDTO("You are not authorized to change the status of this link.", 403, false);
            }
            // === KẾT THÚC SỬA LỖI ===

            if (link.Status != FleetJoinStatus.PENDING)
            {
                return new ResponseDTO("Only PENDING links can have their status changed.", 400, false);
            }

            if (dto.Status != FleetJoinStatus.APPROVED && dto.Status != FleetJoinStatus.REJECTED)
            {
                return new ResponseDTO("Invalid target status. Must be APPROVED or REJECTED.", 400, false);
            }


            link.Status = dto.Status;
            if (dto.Status == FleetJoinStatus.APPROVED)
            {
                link.ApprovedAt = DateTime.UtcNow; 
            }
            else 
            {
                link.ApprovedAt = null;
            }


            try
            {

                await _unitOfWork.OwnerDriverLinkRepo.UpdateAsync(link); 
                await _unitOfWork.SaveChangeAsync(); 
                return new ResponseDTO($"Link status changed to {dto.Status} successfully.", 200, true);
            }
            catch (Exception ex)
            {
                // TODO: Log lỗi chi tiết (ex.Message)
                Console.WriteLine($"Error changing OwnerDriverLink status: {ex.Message}");
                return new ResponseDTO("An error occurred while updating the link status.", 500, false);
            }
        }

        public async Task<ResponseDTO> CreateOwnerDriverLinkAsync(CreateOwerDriverLinkDTO dto)
        {
            var currentDriverId = _userUtility.GetUserIdFromToken();
            if (currentDriverId == Guid.Empty)
            {
                return new ResponseDTO("User is not authenticated.", 401, false);
            }

            // 2. Kiểm tra xem người gửi request có đúng là Driver không (tùy chọn)
            var currentUser = await _unitOfWork.BaseUserRepo.GetByIdAsync(currentDriverId);
            // Cần Include Role nếu bạn kiểm tra RoleName
            //var currentUser = await _unitOfWork.BaseUserRepo.FirstOrDefaultAsync(u => u.UserId == currentDriverId, "Role");
            //if (currentUser == null || currentUser.Role.RoleName != "Driver")
            //{
            //    return new ResponseDTO("Only drivers can send join requests.", 403, false);
            //}


            var owner = await _unitOfWork.OwnerRepo.GetByIdAsync(dto.OwnerId);
            if (owner == null)
            {
                // Nên dùng 404 Not Found
                return new ResponseDTO($"Owner with ID {dto.OwnerId} not found.", 404, false);
            }


            bool linkExists = await _unitOfWork.OwnerDriverLinkRepo.CheckLinkExistsAsync(currentDriverId, dto.OwnerId);
            if (linkExists)
            {
                // Nên dùng 409 Conflict
                return new ResponseDTO("A pending or approved link already exists between this driver and owner.", 409, false);
            }

            var newOwnerDriverLink = new OwnerDriverLink
            {
                OwnerDriverLinkId = Guid.NewGuid(),
                Status = FleetJoinStatus.PENDING, 
                RequestedAt = DateTime.UtcNow,
                ApprovedAt = null,
                OwnerId = dto.OwnerId,
                DriverId = currentDriverId,
            };

            try
            {
                await _unitOfWork.OwnerDriverLinkRepo.AddAsync(newOwnerDriverLink);
                await _unitOfWork.SaveChangeAsync();


                return new ResponseDTO("Join request sent successfully.", 201, true, new { LinkId = newOwnerDriverLink.OwnerDriverLinkId }); // Trả về ID nếu cần
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating OwnerDriverLink: {ex.Message}");
                return new ResponseDTO("An error occurred while creating the link.", 500, false);
            }
        }

        //public async Task<ResponseDTO> GetDriversByOwnerAsync(int pageNumber = 1, int pageSize = 10)
        //{
        //    try
        //    {
        //        // 1. Lấy OwnerId từ Token và xác thực
        //        var ownerId = _userUtility.GetUserIdFromToken();
        //        var userRole = _userUtility.GetUserRoleFromToken();
        //        if (ownerId == Guid.Empty || userRole != "Owner")
        //        {
        //            return new ResponseDTO("Unauthorized: Chỉ 'Owner' mới có thể xem danh sách này.", 401, false);
        //        }

        //        // 2. Lấy IQueryable để phân trang
        //        var query = _unitOfWork.OwnerDriverLinkRepo.GetAll()
        //            .Where(link => link.OwnerId == ownerId);

        //        // 3. Đếm tổng số lượng (cho phân trang)
        //        var totalCount = await query.CountAsync();
        //        if (totalCount == 0)
        //        {
        //            return new ResponseDTO("No drivers found for this owner.", 404, false);
        //        }

        //        // 4. Lấy dữ liệu (Sử dụng .Select() để tối ưu)
        //        var links = await query
        //            .OrderByDescending(l => l.RequestedAt)
        //            .Skip((pageNumber - 1) * pageSize)
        //            .Take(pageSize)
        //            .Select(link => new LinkedDriverDTO // Map sang DTO
        //            {
        //                OwnerDriverLinkId = link.OwnerDriverLinkId,
        //                DriverId = link.DriverId,
        //                FullName = link.Driver.FullName,
        //                PhoneNumber = link.Driver.PhoneNumber,
        //                AvatarUrl = link.Driver.AvatarUrl,
        //                LicenseNumber = link.Driver.LicenseNumber,
        //                Status = link.Status.ToString(), // Trả về "PENDING", "APPROVED", ...
        //                RequestedAt = link.RequestedAt,
        //                ApprovedAt = link.ApprovedAt
        //            })
        //            .ToListAsync();

        //        // 5. Trả về kết quả phân trang
        //        var paginatedResult = new PaginatedDTO<LinkedDriverDTO>(
        //            links, totalCount, pageNumber, pageSize
        //        );

        //        return new ResponseDTO("Get linked drivers successfully.", 200, true, paginatedResult);
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error getting linked drivers: {ex.Message}");
        //        return new ResponseDTO($"Error getting drivers: {ex.Message}", 500, false);
        //    }
        //}


        public async Task<ResponseDTO> GetDriversByOwnerAsync(int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                // 1. Lấy OwnerId từ Token và xác thực
                var ownerId = _userUtility.GetUserIdFromToken();
                // var userRole = _userUtility.GetUserRoleFromToken(); // Tùy logic của bạn có cần check role không

                if (ownerId == Guid.Empty)
                {
                    return new ResponseDTO("Unauthorized.", 401, false);
                }

                // 2. Lấy Query (Include Driver để lấy thông tin cá nhân)
                var query = _unitOfWork.OwnerDriverLinkRepo.GetAll()
                    .Include(l => l.Driver)
                    .Where(link => link.OwnerId == ownerId);

                // 3. Đếm tổng
                var totalCount = await query.CountAsync();
                if (totalCount == 0)
                {
                    // Trả về list rỗng thay vì 404 để Frontend dễ xử lý bảng
                    return new ResponseDTO("No drivers found.", 200, true, new PaginatedDTO<LinkedDriverDTO>(new List<LinkedDriverDTO>(), 0, pageNumber, pageSize));
                }

                // 4. Lấy danh sách Link (Phân trang)
                // LƯU Ý: Ta chưa Select sang DTO ngay, vì cần tính toán Async cho từng driver
                var links = await query
                    .OrderByDescending(l => l.RequestedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // 5. Map sang DTO và Tính toán giờ chạy cho từng tài xế
                var resultList = new List<LinkedDriverDTO>();

                foreach (var link in links)
                {
                    // Gọi hàm tính toán giờ (Ngày/Tuần/Tháng)
                    var stats = await CalculateDriverStatisticsAsync(link.DriverId);

                    resultList.Add(new LinkedDriverDTO
                    {
                        OwnerDriverLinkId = link.OwnerDriverLinkId,
                        DriverId = link.DriverId,
                        FullName = link.Driver.FullName,
                        PhoneNumber = link.Driver.PhoneNumber,
                        AvatarUrl = link.Driver.AvatarUrl,
                        LicenseNumber = link.Driver.LicenseNumber,
                        Status = link.Status.ToString(),
                        RequestedAt = link.RequestedAt,
                        ApprovedAt = link.ApprovedAt,

                        // Map dữ liệu thống kê
                        HoursDrivenToday = stats.HoursToday,
                        HoursDrivenThisWeek = stats.HoursWeek,
                        HoursDrivenThisMonth = stats.HoursMonth,
                        CanDrive = stats.CanDrive
                    });
                }

                // 6. Trả về kết quả
                var paginatedResult = new PaginatedDTO<LinkedDriverDTO>(
                    resultList, totalCount, pageNumber, pageSize
                );

                return new ResponseDTO("Get linked drivers successfully.", 200, true, paginatedResult);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting drivers: {ex.Message}", 500, false);
            }
        }

        // =================================================================
        // HELPER: Tính toán giờ chạy (Ngày, Tuần, Tháng)
        // =================================================================
        private async Task<(double HoursToday, double HoursWeek, double HoursMonth, bool CanDrive)> CalculateDriverStatisticsAsync(Guid driverId)
        {
            var now = DateTime.UtcNow;

            // 1. Xác định các mốc thời gian
            // -- Ngày
            var startOfDay = now.Date;
            var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

            // -- Tuần (Thứ 2 -> Chủ nhật)
            int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
            var startOfWeek = startOfDay.AddDays(-1 * diff);
            var endOfWeek = startOfWeek.AddDays(7).AddTicks(-1);

            // -- Tháng (Ngày 1 -> Cuối tháng)
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddTicks(-1);

            // 2. Xác định phạm vi truy vấn (Lấy mốc xa nhất trong quá khứ)
            // Thường thì StartOfMonth sẽ xa nhất hoặc StartOfWeek (nếu đầu tháng rơi vào giữa tuần)
            var minDate = startOfMonth < startOfWeek ? startOfMonth : startOfWeek;

            // 3. Query dữ liệu (Lấy tất cả các session có dính dáng tới khoảng thời gian này)
            var sessions = await _unitOfWork.DriverWorkSessionRepo.GetAll()
                .Where(s => s.DriverId == driverId
                            && s.StartTime < endOfDay // Bắt đầu trước khi kết thúc hôm nay
                            && (s.EndTime == null || s.EndTime > minDate)) // Kết thúc sau mốc thời gian xa nhất
                .ToListAsync();

            double hToday = 0;
            double hWeek = 0;
            double hMonth = 0;

            foreach (var s in sessions)
            {
                var sEnd = s.EndTime ?? now; // Nếu đang chạy thì tính tới hiện tại

                // A. Tính giờ NGÀY
                var overlapStartDay = s.StartTime > startOfDay ? s.StartTime : startOfDay;
                var overlapEndDay = sEnd < endOfDay ? sEnd : endOfDay;
                if (overlapEndDay > overlapStartDay)
                    hToday += (overlapEndDay - overlapStartDay).TotalHours;

                // B. Tính giờ TUẦN
                var overlapStartWeek = s.StartTime > startOfWeek ? s.StartTime : startOfWeek;
                var overlapEndWeek = sEnd < endOfWeek ? sEnd : endOfWeek;
                if (overlapEndWeek > overlapStartWeek)
                    hWeek += (overlapEndWeek - overlapStartWeek).TotalHours;

                // C. Tính giờ THÁNG
                var overlapStartMonth = s.StartTime > startOfMonth ? s.StartTime : startOfMonth;
                var overlapEndMonth = sEnd < endOfMonth ? sEnd : endOfMonth;
                if (overlapEndMonth > overlapStartMonth)
                    hMonth += (overlapEndMonth - overlapStartMonth).TotalHours;
            }

            // 4. Check điều kiện lái xe (Luật 10h/ngày, 48h/tuần)
            bool canDrive = true;
            if (hToday >= 10 || hWeek >= 48)
            {
                canDrive = false;
            }

            // Làm tròn 1 chữ số thập phân cho đẹp
            return (
                Math.Round(hToday, 1),
                Math.Round(hWeek, 1),
                Math.Round(hMonth, 1),
                canDrive
            );
        }

    }
}