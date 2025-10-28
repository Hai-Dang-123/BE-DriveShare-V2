using Common.Enums.Type; 
using Common.ValueObjects; 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    
    public class TripContact
    {
        public Guid TripContactId { get; set; }

        // Mối quan hệ n-1: Liên kết về Chuyến đi
        public Guid TripId { get; set; } // FK to Trip

        // Loại liên hệ: SENDER (Người gửi) hoặc RECEIVER (Người nhận)
        public ContactType Type { get; set; }

        // --- Thông tin liên hệ ---
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }

        // Ghi chú thêm (ví dụ: "Chỉ giao hàng giờ hành chính")
        public string? Note { get; set; }

        // --- Thuộc tính điều hướng (Navigation Property) ---
        public virtual Trip Trip { get; set; } = null!;
    }
}