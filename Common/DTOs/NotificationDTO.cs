using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class NotificationDTO
    {
        public Guid NotificationId { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string Data { get; set; } // JSON string để FE navigate
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PaginatedNotificationDTO
    {
        public List<NotificationDTO> Items { get; set; }
        public int TotalCount { get; set; }
        public int UnreadCount { get; set; }
    }
}
