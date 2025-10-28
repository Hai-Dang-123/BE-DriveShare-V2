using Common.Enums.Status;
using Common.Enums.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class Trip
    {
        public Guid TripId { get; set; }
        public string TripCode { get; set; } = string.Empty;
        public TripStatus Status { get; set; }
        public DateTime CreateAt { get; set; } = DateTime.Now;
        public DateTime UpdateAt { get; set; } = DateTime.Now;

        public TripType Type { get; set; } // Enum (OwnerCreated, FromProvider)

        // --- Chi phí & Thời gian thực tế ---
        public decimal ActualDistanceKm { get; set; }
        public TimeSpan ActualDuration { get; set; }
        public decimal TotalFare { get; set; } // Tổng cước phí (Owner thu)
        public DateTime? ActualPickupTime { get; set; } // Thời điểm tài xế nhận hàng
        public DateTime? ActualCompletedTime { get; set; } // Thời điểm tài xế giao xong

        // --- GỢI Ý BẮT BUỘC (Khóa ngoại - Dựa trên sơ đồ quan hệ) ---
        public Guid VehicleId { get; set; }
        public virtual Vehicle Vehicle { get; set; } = null!;

        // 1. Chuyến đi này của ai? (Owner - Trip 1-n)
        public Guid OwnerId { get; set; } // FK to Owner

        // 2. Chuyến đi này chở gói hàng nào? (Trip - Package 1-1)

        // 3. Lộ trình (dự kiến) của chuyến đi? (Trip - ShippingRoute 1-1)
        public Guid ShippingRouteId { get; set; } // FK to ShippingRoute

        // 4. Lộ trình (chi tiết từ API map) của chuyến đi? (Trip - TripRoute 1-1)
        public Guid TripRouteId { get; set; } // FK to TripRoute


        // --- GỢI Ý BẮT BUỘC (Thuộc tính điều hướng) ---

        // Liên kết n-1
        public virtual Owner Owner { get; set; } = null!;



        // Liên kết 1-1
        public virtual ICollection<Package> Packages { get; set; } = new List<Package>();
        public virtual ShippingRoute ShippingRoute { get; set; } = null!;
        public virtual TripRoute TripRoute { get; set; } = null!;
        public virtual PostTrip? PostTrip { get; set; } // Bài đăng tuyển tài xế (nếu có)

        // Liên kết 1-n
        public virtual ICollection<DriverWorkSession> DriverWorkSessions { get; set; } = new List<DriverWorkSession>();
        public virtual ICollection<TripDriverAssignment> DriverAssignments { get; set; } = new List<TripDriverAssignment>();
        public virtual ICollection<TripContact> TripContacts { get; set; } = new List<TripContact>();
        public virtual ICollection<TripRouteSuggestion> TripRouteSuggestions { get; set; } = new List<TripRouteSuggestion>();
        public virtual ICollection<TripDeliveryRecord> TripDeliveryRecords { get; set; } = new List<TripDeliveryRecord>();
        public virtual ICollection<TripCompensation> Compensations { get; set; } = new List<TripCompensation>();

        // (Liên kết hợp đồng - đã có trong BaseContract qua TripId)
        public virtual ICollection<TripDriverContract> DriverContracts { get; set; } = new List<TripDriverContract>();
        public virtual ICollection<TripProviderContract> ProviderContracts { get; set; } = new List<TripProviderContract>();

        // (Liên kết giao dịch - đã có trong Transaction qua TripId)
        public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
        public virtual ICollection<TripDeliveryIssue> DeliveryIssues { get; set;} = new List<TripDeliveryIssue>();
    }
}