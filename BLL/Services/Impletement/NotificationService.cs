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

        // =========================================================================
        // PHẦN 1: GỬI & ĐĂNG KÝ (CORE LOGIC)
        // =========================================================================

        // 1. Đăng ký Token (API gọi)
        public async Task<bool> RegisterDeviceTokenAsync(Guid userId, string token, string platform)
        {
            try
            {
                var existingToken = await _unitOfWork.UserDeviceTokenRepo.GetByTokenAsync(token);

                if (existingToken != null)
                {
                    // Update user sở hữu token này
                    existingToken.UserId = userId;
                    existingToken.LastUpdated = DateTime.UtcNow;
                    existingToken.Platform = platform;
                    await _unitOfWork.UserDeviceTokenRepo.UpdateAsync(existingToken);
                }
                else
                {
                    // Tạo mới
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
                Console.WriteLine($"Error registering token: {ex.Message}");
                return false;
            }
        }

        // 2. Gửi cho 1 User (Đơn lẻ)
        public async Task SendToUserAsync(Guid userId, string title, string body, Dictionary<string, string> data = null)
        {
            try
            {
                // A. Lưu DB
                var notiEntity = new DAL.Entities.Notification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = userId,
                    Title = title,
                    Body = body,
                    Data = data != null ? JsonSerializer.Serialize(data) : null,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.NotificationRepo.AddAsync(notiEntity);
                await _unitOfWork.SaveChangeAsync();

                // B. Gửi Firebase
                var tokens = await _unitOfWork.UserDeviceTokenRepo.GetAll()
                    .Where(t => t.UserId == userId)
                    .Select(t => t.DeviceToken)
                    .ToListAsync();

                if (tokens.Any())
                {
                    var message = new MulticastMessage()
                    {
                        Tokens = tokens,
                        Notification = new FirebaseAdmin.Messaging.Notification
                        {
                            Title = title,
                            Body = body
                        },
                        Data = data
                    };
                    // Bắn tin đi
                    await FirebaseMessaging.DefaultInstance.SendMulticastAsync(message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending notification to user {userId}: {ex.Message}");
            }
        }

        // 3. Gửi cho Role (Tối ưu hóa Bulk Insert & Batch Send)
        public async Task SendToRoleAsync(string roleName, string title, string body, Dictionary<string, string> data = null)
        {
            try
            {
                // A. Lấy Data (Dùng AsNoTracking để nhanh)
                var role = await _unitOfWork.RoleRepo.GetByName(roleName);
                if (role == null) return;

                // Lấy UserId và Token của user thuộc role đó (Active only)
                var usersData = await _unitOfWork.BaseUserRepo.GetAll()
                    .AsNoTracking()
                    .Where(u => u.RoleId == role.RoleId && u.Status == UserStatus.ACTIVE)
                    .Select(u => new
                    {
                        u.UserId,
                        Tokens = _unitOfWork.UserDeviceTokenRepo.GetAll().Where(t => t.UserId == u.UserId).Select(t => t.DeviceToken).ToList()
                    })
                    .ToListAsync();

                if (!usersData.Any()) return;

                // B. Lưu DB (Bulk Insert - 1 lệnh duy nhất)
                var notiList = new List<DAL.Entities.Notification>();
                string jsonData = data != null ? JsonSerializer.Serialize(data) : null;
                var now = DateTime.UtcNow;

                foreach (var user in usersData)
                {
                    notiList.Add(new DAL.Entities.Notification
                    {
                        NotificationId = Guid.NewGuid(),
                        UserId = user.UserId,
                        Title = title,
                        Body = body,
                        Data = jsonData,
                        IsRead = false,
                        CreatedAt = now
                    });
                }

                await _unitOfWork.NotificationRepo.AddRangeAsync(notiList);
                await _unitOfWork.SaveChangeAsync();

                // C. Gửi Firebase (Batch Send - Gửi chùm 500 cái)
                var allTokens = usersData
                    .SelectMany(u => u.Tokens)
                    .Where(t => !string.IsNullOrEmpty(t))
                    .Distinct()
                    .ToList();

                if (allTokens.Any())
                {
                    var batches = allTokens.Chunk(500).ToList();
                    foreach (var batch in batches)
                    {
                        try
                        {
                            var message = new MulticastMessage()
                            {
                                Tokens = batch,
                                Notification = new FirebaseAdmin.Messaging.Notification
                                {
                                    Title = title,
                                    Body = body
                                },
                                Data = data
                            };
                            await FirebaseMessaging.DefaultInstance.SendMulticastAsync(message);
                        }
                        catch (Exception batchEx)
                        {
                            Console.WriteLine($"Error sending batch FCM: {batchEx.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendToRoleAsync: {ex.Message}");
            }
        }

        // =========================================================================
        // PHẦN 2: CRUD (GET, PUT, DELETE) - GIỮ NGUYÊN CODE CỦA BẠN (OK RỒI)
        // =========================================================================

        public async Task<ResponseDTO> GetMyNotificationsAsync(Guid userId, int pageNumber, int pageSize)
        {
            try
            {
                var query = _unitOfWork.NotificationRepo.GetAll().Where(n => n.UserId == userId);
                var totalCount = await query.CountAsync();
                var unreadCount = await query.CountAsync(n => !n.IsRead);

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
            catch (Exception ex) { return new ResponseDTO($"Error: {ex.Message}", 500, false); }
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
            catch (Exception ex) { return new ResponseDTO($"Error: {ex.Message}", 500, false); }
        }

        public async Task<ResponseDTO> MarkAsReadAsync(Guid notificationId, Guid userId)
        {
            try
            {
                var noti = await _unitOfWork.NotificationRepo.GetByIdAsync(notificationId);
                if (noti == null) return new ResponseDTO("Notification not found", 404, false);
                if (noti.UserId != userId) return new ResponseDTO("Unauthorized", 403, false);

                if (!noti.IsRead)
                {
                    noti.IsRead = true;
                    await _unitOfWork.NotificationRepo.UpdateAsync(noti);
                    await _unitOfWork.SaveChangeAsync();
                }
                return new ResponseDTO("Marked as read", 200, true);
            }
            catch (Exception ex) { return new ResponseDTO($"Error: {ex.Message}", 500, false); }
        }

        public async Task<ResponseDTO> MarkAllAsReadAsync(Guid userId)
        {
            try
            {
                var unreadNotifications = await _unitOfWork.NotificationRepo.GetAll()
                    .Where(n => n.UserId == userId && !n.IsRead)
                    .ToListAsync();

                if (unreadNotifications.Any())
                {
                    foreach (var noti in unreadNotifications)
                    {
                        noti.IsRead = true;
                        // Chỉ track change, không gọi Save trong loop
                        await _unitOfWork.NotificationRepo.UpdateAsync(noti);
                    }
                    await _unitOfWork.SaveChangeAsync();
                }
                return new ResponseDTO("All marked as read", 200, true);
            }
            catch (Exception ex) { return new ResponseDTO($"Error: {ex.Message}", 500, false); }
        }

        public async Task<ResponseDTO> DeleteNotificationAsync(Guid notificationId, Guid userId)
        {
            try
            {
                var noti = await _unitOfWork.NotificationRepo.GetByIdAsync(notificationId);
                if (noti == null) return new ResponseDTO("Notification not found", 404, false);
                if (noti.UserId != userId) return new ResponseDTO("Unauthorized", 403, false);

                await _unitOfWork.NotificationRepo.DeleteAsync(noti.NotificationId);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO("Deleted successfully", 200, true);
            }
            catch (Exception ex) { return new ResponseDTO($"Error: {ex.Message}", 500, false); }
        }
    }
}