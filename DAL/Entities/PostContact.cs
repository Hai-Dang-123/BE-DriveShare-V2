using Common.Enums.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class PostContact
    {
            public Guid PostContactId { get; set; }

            // Mối quan hệ n-1: Liên kết về Bài đăng
            public Guid PostPackageId { get; set; } // FK to PostPackage
            public virtual PostPackage PostPackage { get; set; } = null!;

            // Loại liên hệ: SENDER (Người gửi) hoặc RECEIVER (Người nhận)
            public ContactType Type { get; set; }

            // --- Thông tin liên hệ ---
            public string FullName { get; set; } = string.Empty;
            public string PhoneNumber { get; set; } = string.Empty;
            public string? Email { get; set; }
            public string? Note { get; set; }

            // (Bạn cũng có thể thêm Address ở đây nếu cần)
        }
    
}
