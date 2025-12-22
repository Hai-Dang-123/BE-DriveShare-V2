using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    // Model tổng chứa kết quả quyết toán của cả chuyến đi
    public class LiquidationResultModel
    {
        public Guid TripId { get; set; }
        public string TripCode { get; set; }
        public DateTime CompletedDate { get; set; }
        // Report chi tiết cho từng đối tượng
        public ParticipantFinancialReport OwnerReport { get; set; }
        public ParticipantFinancialReport ProviderReport { get; set; }
        public List<ParticipantFinancialReport> DriverReports { get; set; } = new();
    }

    // Report chi tiết cho 1 người (Dùng để hiển thị trong Email)
    public class ParticipantFinancialReport
    {
        public Guid UserId { get; set; }
        public string Role { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; } // Quan trọng: Để gửi Email
        public List<FinancialItem> Items { get; set; } = new List<FinancialItem>();

        // Tổng thực nhận = Tổng các khoản (Lương - Phạt + Bồi thường...)
        public decimal FinalAmount => Items.Sum(x => x.Amount);

        public void AddItem(string description, decimal amount, bool isDeduction)
        {
            Items.Add(new FinancialItem { Description = description, Amount = amount, IsDeduction = isDeduction });
        }
    }
    public class FinancialItem
    {
        public string Description { get; set; }
        public decimal Amount { get; set; } // Số âm nếu là phạt, dương nếu là thu nhập
        public bool IsDeduction { get; set; } // Cờ để UI hiển thị màu đỏ/xanh
    }
    public class FinancialLineItem
    {
        public string Description { get; set; } // VD: "Lương cứng", "Phạt hư xe"
        public decimal Amount { get; set; }     // Giá trị tuyệt đối
        public bool IsNegative { get; set; }    // True = Màu đỏ (Trừ), False = Màu xanh (Cộng)
    }
    // Các model phụ như TripCompletionReportModel, SurchargeDetail, ExpenseDetail giữ nguyên như bài trước.
}
