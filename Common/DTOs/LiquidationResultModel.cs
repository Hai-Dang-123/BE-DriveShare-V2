using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class LiquidationResultModel
    {
        public Guid TripId { get; set; }
        public string TripCode { get; set; }
        public Guid OwnerId { get; set; }
        public decimal OwnerReceived { get; set; }
        public decimal ProviderPaid { get; set; } // Tổng tiền Provider đã trả ban đầu

        // Map DriverId -> Số tiền thực nhận (Nếu âm là nợ)
        public Dictionary<Guid, decimal> PaidDriversMap { get; set; } = new Dictionary<Guid, decimal>();
        public List<SurchargeDetail> Surcharges { get; set; } = new List<SurchargeDetail>();
    }

    // Các model phụ như TripCompletionReportModel, SurchargeDetail, ExpenseDetail giữ nguyên như bài trước.
}
