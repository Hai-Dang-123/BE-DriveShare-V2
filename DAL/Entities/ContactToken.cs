using Common.Enums.Type;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class ContactToken
    {
        [Key]
        public Guid ContactTokenId { get; set; }

        // Liên kết với TripContact (Người nhận/Gửi)
        public Guid TripContactId { get; set; }

        public string TokenValue { get; set; } = null!; // Lưu mã OTP đã Hash
        public TokenType TokenType { get; set; } // DELIVERY_RECORD_OTP, ACCESS_TOKEN...

        public bool IsRevoked { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiredAt { get; set; }

        // Navigation prop
        [ForeignKey("TripContactId")]
        public virtual TripContact TripContact { get; set; } = null!;
    }
}
