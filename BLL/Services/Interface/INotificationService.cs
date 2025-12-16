using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface INotificationService
    {
        Task<bool> RegisterDeviceTokenAsync(Guid userId, string token, string platform);
        Task SendToUserAsync(Guid userId, string title, string body, Dictionary<string, string> data = null);
        Task SendToRoleAsync(string roleName, string title, string body, Dictionary<string, string> data = null);

        // --- CÁC HÀM MỚI BỔ SUNG ---
        Task<ResponseDTO> GetMyNotificationsAsync(Guid userId, int pageNumber, int pageSize);
        Task<ResponseDTO> GetUnreadCountAsync(Guid userId);
        Task<ResponseDTO> MarkAsReadAsync(Guid notificationId, Guid userId);
        Task<ResponseDTO> MarkAllAsReadAsync(Guid userId);
        Task<ResponseDTO> DeleteNotificationAsync(Guid notificationId, Guid userId);
    }
}
