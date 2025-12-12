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

        // Report chi tiết cho từng đối tượng
        public ParticipantFinancialReport OwnerReport { get; set; }
        public ParticipantFinancialReport ProviderReport { get; set; }
        public List<ParticipantFinancialReport> DriverReports { get; set; } = new();
    }

    // Report chi tiết cho 1 người (Dùng để hiển thị trong Email)
    public class ParticipantFinancialReport
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; } // "Owner", "Provider", "Main Driver", "Sub Driver"

        // Danh sách các mục chi tiết (VD: +Lương, +Bonus, -Phạt, +Hoàn cọc)
        public List<FinancialLineItem> Items { get; set; } = new List<FinancialLineItem>();

        // Số tiền biến động thực tế trong ví (Cộng/Trừ)
        public decimal FinalWalletChange { get; set; }

        // Helper để add dòng nhanh
        public void AddItem(string desc, decimal amount, bool isDeduction = false)
        {
            if (amount == 0) return; // Không add dòng 0đ
            Items.Add(new FinancialLineItem
            {
                Description = desc,
                Amount = amount,
                IsNegative = isDeduction
            });
        }
    }

    public class FinancialLineItem
    {
        public string Description { get; set; } // VD: "Lương cứng", "Phạt hư xe"
        public decimal Amount { get; set; }     // Giá trị tuyệt đối
        public bool IsNegative { get; set; }    // True = Màu đỏ (Trừ), False = Màu xanh (Cộng)
    }
    // Các model phụ như TripCompletionReportModel, SurchargeDetail, ExpenseDetail giữ nguyên như bài trước.
}
