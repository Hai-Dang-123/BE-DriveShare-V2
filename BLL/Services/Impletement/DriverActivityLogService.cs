using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class DriverActivityLogService : IDriverActivityLogService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;
        public DriverActivityLogService(IUnitOfWork unitOfWork, UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
        }

        // =========================================================================
        // 1. GET MY LOGS (Tài xế xem nhật ký của chính mình)
        // =========================================================================
        public async Task<ResponseDTO> GetMyLogsAsync(int page, int pageSize, string logLevel = null)
        {
            try
            {
                var driverId = _userUtility.GetUserIdFromToken();
                if (driverId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                var query = _unitOfWork.DriverActivityLogRepo.GetAll()
                    .AsNoTracking()
                    .Where(l => l.DriverId == driverId);

                // Filter theo Level (Info, Warning, Critical)
                if (!string.IsNullOrEmpty(logLevel))
                {
                    query = query.Where(l => l.LogLevel == logLevel);
                }

                query = query.OrderByDescending(l => l.CreateAt); // Mới nhất lên đầu

                var totalCount = await query.CountAsync();
                var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
                    .Select(l => new DriverActivityLogDTO
                    {
                        DriverActivityLogId = l.DriverActivityLogId,
                        Description = l.Description,
                        LogLevel = l.LogLevel,
                        CreateAt = l.CreateAt,
                        DriverId = l.DriverId,
                        DriverName = "Me", // Không cần query join tên
                        DriverPhone = ""
                    }).ToListAsync();

                return new ResponseDTO("Success", 200, true, new PaginatedDTO<DriverActivityLogDTO>(items, totalCount, page, pageSize));
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi lấy log cá nhân: {ex.Message}", 500, false);
            }
        }

        // =========================================================================
        // 2. GET LOGS BY DRIVER ID (Admin xem nhật ký của 1 tài xế)
        // =========================================================================
        public async Task<ResponseDTO> GetLogsByDriverIdAsync(Guid driverId, int page, int pageSize, string logLevel = null)
        {
            try
            {
                // Validate Admin Role
                var role = _userUtility.GetUserRoleFromToken();
                if (role != "Admin" && role != "Staff" && role != "Owner")
                    return new ResponseDTO("Forbidden: Không có quyền xem log người khác.", 403, false);

                var query = _unitOfWork.DriverActivityLogRepo.GetAll()
                    .AsNoTracking()
                    .Include(l => l.Driver) // Join để lấy tên
                    .Where(l => l.DriverId == driverId);

                if (!string.IsNullOrEmpty(logLevel))
                {
                    query = query.Where(l => l.LogLevel == logLevel);
                }

                query = query.OrderByDescending(l => l.CreateAt);

                var totalCount = await query.CountAsync();
                var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
                    .Select(l => new DriverActivityLogDTO
                    {
                        DriverActivityLogId = l.DriverActivityLogId,
                        Description = l.Description,
                        LogLevel = l.LogLevel,
                        CreateAt = l.CreateAt,
                        DriverId = l.DriverId,
                        DriverName = l.Driver.FullName,
                        DriverPhone = l.Driver.PhoneNumber
                    }).ToListAsync();

                return new ResponseDTO("Success", 200, true, new PaginatedDTO<DriverActivityLogDTO>(items, totalCount, page, pageSize));
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi lấy log theo ID: {ex.Message}", 500, false);
            }
        }

        // =========================================================================
        // 3. GET ALL LOGS (Admin xem toàn bộ hệ thống)
        // =========================================================================
        public async Task<ResponseDTO> GetAllLogsAsync(int page, int pageSize, string search = null, string logLevel = null)
        {
            try
            {
                var role = _userUtility.GetUserRoleFromToken();
                if (role != "Admin") return new ResponseDTO("Forbidden: Chỉ Admin mới được xem toàn bộ log.", 403, false);

                var query = _unitOfWork.DriverActivityLogRepo.GetAll()
                    .AsNoTracking()
                    .Include(l => l.Driver)
                    .AsQueryable();

                // Filter Level
                if (!string.IsNullOrEmpty(logLevel))
                {
                    query = query.Where(l => l.LogLevel == logLevel);
                }

                // Search (Tìm theo tên tài xế, SĐT hoặc nội dung log)
                if (!string.IsNullOrEmpty(search))
                {
                    string keyword = search.ToLower().Trim();
                    query = query.Where(l =>
                        l.Description.ToLower().Contains(keyword) ||
                        l.Driver.FullName.ToLower().Contains(keyword) ||
                        l.Driver.PhoneNumber.Contains(keyword)
                    );
                }

                query = query.OrderByDescending(l => l.CreateAt);

                var totalCount = await query.CountAsync();
                var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
                    .Select(l => new DriverActivityLogDTO
                    {
                        DriverActivityLogId = l.DriverActivityLogId,
                        Description = l.Description,
                        LogLevel = l.LogLevel,
                        CreateAt = l.CreateAt,
                        DriverId = l.DriverId,
                        DriverName = l.Driver.FullName,
                        DriverPhone = l.Driver.PhoneNumber
                    }).ToListAsync();

                return new ResponseDTO("Success", 200, true, new PaginatedDTO<DriverActivityLogDTO>(items, totalCount, page, pageSize));
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi lấy toàn bộ log: {ex.Message}", 500, false);
            }
        }

        // =========================================================================
        // 4. COUNT / STATISTICS LOG (Thống kê)
        // =========================================================================
        public async Task<ResponseDTO> GetLogStatisticsAsync(Guid? driverId = null)
        {
            try
            {
                var query = _unitOfWork.DriverActivityLogRepo.GetAll().AsNoTracking();

                // Nếu truyền driverId -> thống kê cho user đó. Nếu null -> thống kê toàn hệ thống
                if (driverId.HasValue && driverId != Guid.Empty)
                {
                    query = query.Where(l => l.DriverId == driverId.Value);
                }
                else
                {
                    // Nếu không truyền ID, phải là Admin mới xem được thống kê tổng
                    var role = _userUtility.GetUserRoleFromToken();
                    if (role != "Admin") return new ResponseDTO("Forbidden", 403, false);
                }

                // Dùng GroupBy hoặc Count trực tiếp (Count trực tiếp dễ viết hơn cho EF Core basic)
                var stats = new LogStatisticsDTO
                {
                    TotalLogs = await query.CountAsync(),
                    InfoCount = await query.CountAsync(l => l.LogLevel == "Info"),
                    WarningCount = await query.CountAsync(l => l.LogLevel == "Warning"),
                    CriticalCount = await query.CountAsync(l => l.LogLevel == "Critical")
                };

                return new ResponseDTO("Statistics retrieved successfully", 200, true, stats);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi thống kê log: {ex.Message}", 500, false);
            }
        }
    }
}
