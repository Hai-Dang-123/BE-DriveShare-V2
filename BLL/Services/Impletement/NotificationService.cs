using BLL.Services.Interface;
using Common.DTOs;
using Common.Enums.Status;
using DAL.Entities;
using DAL.UnitOfWork;
using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _unitOfWork;

        public NotificationService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // 1. Hàm đăng ký Token (Dùng cho API gọi vào)
        public async Task<bool> RegisterDeviceTokenAsync(Guid userId, string token, string platform)
        {
            try
            {
                // Logic: 
                // - 1 Token chỉ thuộc về 1 User tại 1 thời điểm.
                // - Nếu Token đã có trong DB rồi -> Update UserId mới (trường hợp đăng nhập nick khác trên máy cũ)
                // - Nếu chưa có -> Thêm mới.

                var existingToken = await _unitOfWork.UserDeviceTokenRepo.GetByTokenAsync(token);

                if (existingToken != null)
                {
                    existingToken.UserId = userId;
                    existingToken.LastUpdated = DateTime.UtcNow;
                    existingToken.Platform = platform;
                    await _unitOfWork.UserDeviceTokenRepo.UpdateAsync(existingToken);
                }
                else
                {
                    var newToken = new UserDeviceToken
                    {
                        UserDeviceTokenId = Guid.NewGuid(),
                        UserId = userId,
                        DeviceToken = token,
                        Platform = platform,
                        LastUpdated = DateTime.UtcNow
                    };
                    await _unitOfWork.UserDeviceTokenRepo.AddAsync(newToken);
                }

                await _unitOfWork.SaveChangeAsync();
                return true;
            }
            catch (Exception ex)
            {
                // Log error here
                return false;
            }
        }

        // 2. Gửi cho 1 User (Sửa lại)
        public async Task SendToUserAsync(Guid userId, string title, string body, Dictionary<string, string> data = null)
        {
            try
            {
                // --- BƯỚC A: LƯU VÀO DATABASE ---
                var notiEntity = new DAL.Entities.Notification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = userId,
                    Title = title,
                    Body = body,
                    Data = data != null ? JsonSerializer.Serialize(data) : null, // Lưu data dạng chuỗi JSON
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.NotificationRepo.AddAsync(notiEntity);
                await _unitOfWork.SaveChangeAsync(); // Lưu xong mới bắn noti để đảm bảo dữ liệu toàn vẹn

                // --- BƯỚC B: GỬI FCM (Như cũ) ---
                var tokens = await _unitOfWork.UserDeviceTokenRepo.GetAll()
                    .Where(t => t.UserId == userId)
                    .Select(t => t.DeviceToken)
                    .ToListAsync();

                if (tokens.Any())
                {
                    var message = new MulticastMessage()
                    {
                        Tokens = tokens,
                        Notification = new FirebaseAdmin.Messaging.Notification()
                        {
                            Title = title,
                            Body = body
                        },
                        Data = data
                    };
                    await FirebaseMessaging.DefaultInstance.SendMulticastAsync(message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving/sending notification: {ex.Message}");
            }
        }

        // 3. Gửi cho Role (Sửa lại: Phải loop để lưu từng bản ghi cho từng người)
        public async Task SendToRoleAsync(string roleName, string title, string body, Dictionary<string, string> data = null)
        {
            // Lấy list UserID
            var userIds = await _unitOfWork.BaseUserRepo.GetAll()
                .Where(u => u.Role.RoleName == roleName && u.Status == UserStatus.ACTIVE)
                .Select(u => u.UserId)
                .ToListAsync();

            // Loop để gọi hàm trên (Vừa lưu DB riêng cho từng người, vừa bắn Noti)
            foreach (var uid in userIds)
            {
                // Fire-and-forget từng task để chạy nhanh
                _ = SendToUserAsync(uid, title, body, data);
            }
        }

        // =========================================================================
        // PHẦN 2: CÁC HÀM MỚI (GET, PUT, DELETE)
        // =========================================================================

        public async Task<ResponseDTO> GetMyNotificationsAsync(Guid userId, int pageNumber, int pageSize)
        {
            try
            {
                var query = _unitOfWork.NotificationRepo.GetAll().Where(n => n.UserId == userId);

                // Đếm tổng
                var totalCount = await query.CountAsync();
                var unreadCount = await query.CountAsync(n => !n.IsRead);

                // Phân trang
                var items = await query
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(n => new NotificationDTO
                    {
                        NotificationId = n.NotificationId,
                        Title = n.Title,
                        Body = n.Body,
                        Data = n.Data,
                        IsRead = n.IsRead,
                        CreatedAt = n.CreatedAt
                    })
                    .ToListAsync();

                var result = new PaginatedNotificationDTO
                {
                    Items = items,
                    TotalCount = totalCount,
                    UnreadCount = unreadCount
                };

                return new ResponseDTO("Success", 200, true, result);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> GetUnreadCountAsync(Guid userId)
        {
            try
            {
                var count = await _unitOfWork.NotificationRepo.GetAll()
                    .Where(n => n.UserId == userId && !n.IsRead)
                    .CountAsync();

                return new ResponseDTO("Success", 200, true, new { UnreadCount = count });
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> MarkAsReadAsync(Guid notificationId, Guid userId)
        {
            try
            {
                var noti = await _unitOfWork.NotificationRepo.GetByIdAsync(notificationId);

                if (noti == null)
                    return new ResponseDTO("Notification not found", 404, false);

                if (noti.UserId != userId)
                    return new ResponseDTO("Unauthorized", 403, false);

                if (!noti.IsRead)
                {
                    noti.IsRead = true;
                    await _unitOfWork.NotificationRepo.UpdateAsync(noti);
                    await _unitOfWork.SaveChangeAsync();
                }

                return new ResponseDTO("Marked as read", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> MarkAllAsReadAsync(Guid userId)
        {
            try
            {
                // Lấy tất cả tin chưa đọc của user
                var unreadNotifications = await _unitOfWork.NotificationRepo.GetAll()
                    .Where(n => n.UserId == userId && !n.IsRead)
                    .ToListAsync();

                if (unreadNotifications.Any())
                {
                    foreach (var noti in unreadNotifications)
                    {
                        noti.IsRead = true;
                        // Không gọi SaveChangeAsync trong loop để tối ưu
                        await _unitOfWork.NotificationRepo.UpdateAsync(noti);
                    }
                    await _unitOfWork.SaveChangeAsync();
                }

                return new ResponseDTO("All marked as read", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> DeleteNotificationAsync(Guid notificationId, Guid userId)
        {
            try
            {
                var noti = await _unitOfWork.NotificationRepo.GetByIdAsync(notificationId);

                if (noti == null)
                    return new ResponseDTO("Notification not found", 404, false);

                if (noti.UserId != userId)
                    return new ResponseDTO("Unauthorized", 403, false);

                await _unitOfWork.NotificationRepo.DeleteAsync(noti.NotificationId);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Deleted successfully", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error: {ex.Message}", 500, false);
            }
        }
    }
}
