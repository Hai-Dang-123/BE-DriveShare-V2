using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    // Model dùng để render Email HTML
    public class TripCompletionReportModel
    {
        public string TripCode { get; set; }
        public string CompletedAt { get; set; }
        public string StartAddress { get; set; }
        public string EndAddress { get; set; }
        public decimal DistanceKm { get; set; }
        public string VehiclePlate { get; set; }
        public string RecipientName { get; set; }
        public string Role { get; set; } // "Owner", "Provider", "Driver"

        // --- TÀI CHÍNH ---
        public decimal Amount { get; set; } // Số tiền chốt (Dương là nhận, Âm là trả/nợ)
        public bool IsIncome { get; set; }  // True: Tiền về ví, False: Tiền trừ ví
        public string FinancialDescription { get; set; }

        // --- DÀNH RIÊNG CHO OWNER (Quyết toán) ---
        public decimal OwnerGrossRevenue { get; set; } // Doanh thu
        public decimal OwnerTotalDriverPay { get; set; } // Chi phí tài xế

        public List<SurchargeDetail> Surcharges { get; set; } = new List<SurchargeDetail>();
        public List<ExpenseDetail> DriverExpenses { get; set; } = new List<ExpenseDetail>();

        public TripCompletionReportModel Clone() => (TripCompletionReportModel)MemberwiseClone();
    }

    public class SurchargeDetail
    {
        public string Type { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
    }

    public class ExpenseDetail
    {
        public string DriverName { get; set; }
        public string Role { get; set; }
        public decimal Amount { get; set; }
    }
}
