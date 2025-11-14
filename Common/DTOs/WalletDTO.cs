using Common.Enums.Type;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class WalletDTO
    {
        public Guid WalletId { get; set; }
        public Guid UserId { get; set; }
        public decimal Balance { get; set; }
        public decimal FrozenBalance { get; set; }
        public string Currency { get; set; } = "VND";
        public string Status { get; set; } = string.Empty;
        public DateTime LastUpdatedAt { get; set; }
    }

    // ⚠️ DTO MỚI: DTO cho yêu cầu rút tiền (từ người dùng)
    public class WithdrawalRequestDTO
    {
        [Required]
        [Range(1, (double)decimal.MaxValue, ErrorMessage = "Amount must be positive")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Description is required")]
        [MaxLength(250)]
        public string Description { get; set; } = string.Empty;
    }

    // ⚠️ DTO MỚI: DTO cho các giao dịch nội bộ (Admin nạp, Service thanh toán)
    public class InternalTransactionRequestDTO
    {

        [Required]
        [Range(1, (double)decimal.MaxValue, ErrorMessage = "Amount must be positive")]
        public decimal Amount { get; set; } // Luôn là số dương (hàm service sẽ xử lý cộng/trừ)

        [Required]
        public TransactionType Type { get; set; }

        public Guid? TripId { get; set; } // Bắt buộc cho Payment/Payout

        [Required]
        public string Description { get; set; } = string.Empty;

        public string? ExternalCode { get; set; }
    }
}
