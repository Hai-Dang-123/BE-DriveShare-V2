using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class TransactionDTO
    {
        public Guid TransactionId { get; set; }
        public Guid WalletId { get; set; }
        public Guid? TripId { get; set; }
        public decimal Amount { get; set; } // Sẽ là số âm nếu là trừ tiền
        public decimal BalanceBefore { get; set; }
        public decimal BalanceAfter { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Description { get; set; }
        public string? ExternalTransactionCode { get; set; }
    }

    // DTO cho phân trang lịch sử giao dịch
    public class TransactionHistoryDTO
    {
        public WalletDTO WalletInfo { get; set; } = null!;
        public PaginatedDTO<TransactionDTO> Transactions { get; set; } = null!;
    }
}
