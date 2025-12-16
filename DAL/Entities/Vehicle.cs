using Common.Enums.Status;
using Common.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class Vehicle
    {
        public Guid VehicleId { get; set; }

        // --- GỢI Ý BẮT BUỘC (Khóa ngoại - Dựa trên sơ đồ) ---

        // 1. Xe này của Chủ xe (Owner) nào? (Owner - Vehicle 1-n)
        public Guid OwnerId { get; set; } // FK to Owner

        // 2. Xe này thuộc Loại xe nào? (Vehicle - VehicleType n-1)
        public Guid VehicleTypeId { get; set; } // FK to VehicleType

        // --- Thông tin cơ bản ---
        public string PlateNumber { get; set; } = null!;
        public string Model { get; set; } = null!;
        public string Brand { get; set; } = null!;
        public int YearOfManufacture { get; set; }
        public string Color { get; set; } = null!;

        // --- Thông số vận tải ---
        public decimal PayloadInKg { get; set; } // Trọng tải (kg)
        public decimal VolumeInM3 { get; set; } // Thể tích thùng (m3)

        // GỢI Ý (Nghiệp vụ): Tính năng của xe (ví dụ: "Xe lạnh", "Xe 2 sàn")
        public List<string> Features { get; set; } = new List<string>();

        // --- Trạng thái ---
        public Location? CurrentAddress { get; set; } // Vị trí hiện tại (nullable)
        public VehicleStatus Status { get; set; } 



        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // --- GỢI Ý BẮT BUỘC (Thuộc tính điều hướng) ---

        // Liên kết n-1
        public virtual Owner Owner { get; set; } = null!;
        public virtual VehicleType VehicleType { get; set; } = null!;

        // Liên kết 1-n
        public virtual ICollection<VehicleDocument> VehicleDocuments { get; set; } = new List<VehicleDocument>();
        public virtual ICollection<VehicleImage> VehicleImages { get; set; } = new List<VehicleImage>();
        public virtual ICollection<Trip> Trips { get; set; } = new List<Trip>();
        public virtual ICollection<TripVehicleHandoverRecord> TripVehicleHandoverRecords { get; set; } = new List<TripVehicleHandoverRecord>();

        // Thêm dòng này: Một xe có nhiều lịch sử đăng kiểm
        public virtual ICollection<InspectionHistory> InspectionHistories { get; set; } = new List<InspectionHistory>();

    }
}