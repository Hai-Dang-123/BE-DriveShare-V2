using Common.Enums.Status;
using Common.Enums.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class Transaction
    {
        public Guid TransactionId { get; set; }

        // Giao dịch này thuộc Wallet nào?
        public Guid WalletId { get; set; } // FK to Wallet

        // Giao dịch này liên quan đến Chuyến đi nào? (Có thể null)
        // Ví dụ: Nạp tiền (TOPUP) thì không cần TripId
        // Thanh toán (TRIP_PAYMENT) thì cần TripId
        public Guid? TripId { get; set; } // FK to Trip
       

        public decimal Amount { get; set; } // Số tiền giao dịch
        public string Currency { get; set; } = "VND";

        // GỢI Ý: Thêm số dư tại thời điểm giao dịch để đối soát
        public decimal BalanceBefore { get; set; }
        public decimal BalanceAfter { get; set; }

        public TransactionType Type { get; set; } // Enum: TOPUP, WITHDRAWAL, TRIP_PAYMENT, DRIVER_PAYOUT, PLATFORM_FEE, REFUND
        public TransactionStatus Status { get; set; } // Enum: PENDING, SUCCEEDED, FAILED

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; } // Thời gian giao dịch thành công/thất bại

        public string? Description { get; set; }
        public string? ExternalTransactionCode { get; set; } // Mã giao dịch của bên thứ 3 (Momo, Bank...)

        // --- Thuộc tính điều hướng (Navigation Properties) ---
        public virtual Wallet Wallet { get; set; } = null!;
        public virtual Trip? Trip { get; set; }

        }
}
