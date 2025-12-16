using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class Notification
    {
        public Guid NotificationId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid UserId { get; set; } // Người nhận


        public string Title { get; set; } = null!;

        public string Body { get; set; } = null!;

        public string? Data { get; set; } // Lưu JSON data (để FE biết bấm vào nhảy đi đâu)

        public bool IsRead { get; set; } = false; // Đã đọc chưa?

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


        public virtual BaseUser User { get; set; } = null!;
    }
}
