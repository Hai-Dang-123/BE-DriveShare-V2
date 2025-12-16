using DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Repositories.Interface
{
    public interface INotificationRepository : IGenericRepository<Notification>
    {
        // Lấy 20 thông báo gần nhất của user
        Task<List<Notification>> GetUserNotifications(Guid userId, int pageIndex, int pageSize);

        // Đếm số lượng chưa đọc
        Task<int> CountUnreadAsync(Guid userId);
    }
}
