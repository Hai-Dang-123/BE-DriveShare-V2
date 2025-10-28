using Common.Enums.Status;
using Common.Enums.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class PostTrip
    {
        public Guid PostTripId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // --- Thông tin đăng tuyển ---
        public decimal EstimatedFare { get; set; } // Tổng tiền dự kiến (trả cho tài xế)
        public PostStatus Status { get; set; } // Open, Filled, Cancelled
        public DateTime CreateAt { get; set; } = DateTime.Now;
        public DateTime UpdateAt { get; set; } = DateTime.Now;

        // --- Yêu cầu cho tài xế/xe ---
        public DriverType Type { get; set; } // Loại tài xế (chính, phụ)
        public decimal? RequiredPayloadInKg { get; set; } // Yêu cầu trọng tải
        public int? RequiredMainDrivers { get; set; }     // SỐ LƯỢNG TÀI CHÍNH CẦN
        public int? RequiredAssistantDrivers { get; set; }// SỐ LƯỢNG TÀI PHỤ CẦN

        // --- GỢI Ý BẮT BUỘC (Dựa trên sơ đồ quan hệ) ---

        // 1. Ai là người đăng bài? (Owner - PostTrip 1-n)
        public Guid OwnerId { get; set; } // FK to Owner
        public virtual Owner Owner { get; set; } = null!;

        // 2. Bài đăng này cho Chuyến đi nào?
        public Guid TripId { get; set; } // FK to Trip
        public virtual Trip Trip { get; set; } = null!;



      
    }
}