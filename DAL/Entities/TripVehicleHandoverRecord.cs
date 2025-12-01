using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{

    public class TripVehicleHandoverRecord : DeliveryRecord
    {
        // DeliveryRecordId, Type (PICKUP/DROPOFF), CreatedAt... được kế thừa từ cha

        // --- KHÓA NGOẠI BẮT BUỘC (Gắn với Trip) ---
        public Guid TripId { get; set; } // FK to Trip

        // Xe nào đang được giao nhận? (Dù Trip có xe rồi nhưng lưu ở đây để snapshot lịch sử)
        public Guid VehicleId { get; set; } // FK to Vehicle

        // --- CÁC BÊN THAM GIA ---
        // 1. Bên Giao (Ví dụ: Chủ xe khi Pickup, hoặc Tài xế khi Dropoff)
        public Guid OwnerId { get; set; }
        public virtual Owner Owner { get; set; }

        // 2. Bên Nhận (Ví dụ: Tài xế khi Pickup, hoặc Chủ xe khi Dropoff)
        public Guid DriverId { get; set; }
        public virtual Driver Driver { get; set; }

        // --- THÔNG SỐ KỸ THUẬT XE (Snapshot tại thời điểm giao/nhận) ---
        public double CurrentOdometer { get; set; } // Số Odometer hiện tại (Km)
        public double FuelLevel { get; set; } // Mức nhiên liệu (0-100% hoặc số Lít)
        public bool IsEngineLightOn { get; set; } // Đèn check engine có sáng không?

        // --- CHỮ KÝ XÁC NHẬN ---
        //public string? HandoverSignatureUrl { get; set; } // Chữ ký bên giao
        public DateTime? OwnerSignedAt { get; set; }

        //public string? ReceiverSignatureUrl { get; set; } // Chữ ký bên nhận
        public DateTime? DriverSignedAt { get; set; }
        public bool OwnerSigned { get; set; }
        public bool DriverSigned { get; set; }

        // --- NAVIGATION PROPERTIES ---
        public virtual Trip Trip { get; set; } = null!;
         public virtual Vehicle Vehicle { get; set; } = null!;
        // public virtual BaseUser HandoverUser { get; set; } = null!;
        // public virtual BaseUser ReceiverUser { get; set; } = null!;

        // --- LIÊN KẾT CHI TIẾT ---
        // 1. Kết quả kiểm tra từng hạng mục (Lốp, Kính, Nội thất...)
        public virtual ICollection<TripVehicleHandoverTermResult> TermResults { get; set; } = new List<TripVehicleHandoverTermResult>();

        // 2. Các vấn đề/hư hỏng phát hiện tại chỗ (Trầy xước, móp...)
        public virtual ICollection<TripVehicleHandoverIssue> Issues { get; set; } = new List<TripVehicleHandoverIssue>();

    }
}
