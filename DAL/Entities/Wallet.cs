using Common.Enums.Status; // Giả sử bạn có Enum 'WalletStatus'
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class Wallet
    {
        public Guid WalletId { get; set; }

        // --- GỢI Ý BẮT BUỘC (Khóa ngoại - Dựa trên sơ đồ 1-1) ---
        // Ví này của ai?
        public Guid UserId { get; set; } // FK to BaseUser

        // --- Chi tiết Tài chính ---
        public decimal Balance { get; set; } = 0; // Số dư khả dụng

        // GỢI Ý (Nghiệp vụ): Số dư đang bị đóng băng (ví dụ: tiền cọc, tiền đang trong 1 trip)
        public decimal FrozenBalance { get; set; } = 0;

        public string Currency { get; set; } = "VND";
        public WalletStatus Status { get; set; } = WalletStatus.ACTIVE; // ACTIVE, FROZEN, CLOSED

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

        // --- GỢI Ý BẮT BUỘC (Thuộc tính điều hướng) ---

        // Mối quan hệ 1-1
        public virtual BaseUser User { get; set; } = null!;

        // Mối quan hệ 1-n (Lịch sử giao dịch của ví này)
        public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}