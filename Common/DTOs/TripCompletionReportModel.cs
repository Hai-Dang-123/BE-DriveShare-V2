using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class TripCompletionReportModel
    {
        // ... Các field cũ giữ nguyên ...
        public string TripCode { get; set; }
        public string CompletedAt { get; set; }
        public string StartAddress { get; set; }
        public string EndAddress { get; set; }
        public double DistanceKm { get; set; }
        public string VehiclePlate { get; set; }
        public string VehicleType { get; set; }
        public int PackageCount { get; set; }
        public decimal TotalPayload { get; set; }
        public string RecipientName { get; set; }
        public string Role { get; set; }

        // --- FIELD CHO PROVIDER / DRIVER (Đơn giản) ---
        public decimal Amount { get; set; }
        public bool IsIncome { get; set; }
        public string FinancialDescription { get; set; }

        // --- [NEW] FIELD DÀNH RIÊNG CHO OWNER (Quyết toán tổng hợp) ---
        public decimal TotalIncome { get; set; }      // Tiền nhận từ Provider (sau phí sàn)
        public decimal TotalExpense { get; set; }     // Tổng tiền trả cho Drivers
        public decimal NetProfit => TotalIncome - TotalExpense; // Lợi nhuận thực
        public List<ExpenseDetail> DriverExpenses { get; set; } = new List<ExpenseDetail>();

        public TripCompletionReportModel Clone() => (TripCompletionReportModel)MemberwiseClone();
    }

    public class ExpenseDetail
    {
        public string DriverName { get; set; }
        public string Role { get; set; } // Tài chính/Tài phụ
        public decimal Amount { get; set; }
    }
}
