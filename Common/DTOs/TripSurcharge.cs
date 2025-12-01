using Common.Enums.Status;
using Common.Enums.Type;
using System;

namespace Common.DTOs
{
    // DTO tạo mới phụ phí
    public class TripSurchargeCreateDTO
    {
        public Guid TripId { get; set; }
        public SurchargeType Type { get; set; } // FUEL, CLEANING, DAMAGE...
        public decimal Amount { get; set; }
        public string Description { get; set; }

        // Optional: Nếu khoản phạt này sinh ra từ một sự cố cụ thể
        public Guid? TripVehicleHandoverIssueId { get; set; } // Sự cố xe
        public Guid? TripDeliveryIssueId { get; set; }        // Sự cố hàng
    }

    // DTO hiển thị danh sách
    public class TripSurchargeReadDTO
    {
        public Guid TripSurchargeId { get; set; }
        public Guid TripId { get; set; }
        public string Type { get; set; } // Trả về string cho FE
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public string Status { get; set; } // PENDING, PAID...
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }

        // Thông tin issue liên quan (nếu có)
        public Guid? RelatedVehicleIssueId { get; set; }
        public Guid? RelatedDeliveryIssueId { get; set; }
    }

    // DTO cập nhật trạng thái (Ví dụ: Xác nhận đã trả tiền)
    public class UpdateSurchargeStatusDTO
    {
        public Guid TripSurchargeId { get; set; }
        public SurchargeStatus NewStatus { get; set; }
    }
}