using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    // TripDeliveryRecordReadDTO.cs (Cập nhật thêm TripInfo)
    public class TripDeliveryRecordReadForDriverDTO
    {
        public Guid TripDeliveryRecordId { get; set; } // Nên có ID chính nó
        public Guid TripId { get; set; }
        public Guid? DeliveryRecordTempalteId { get; set; }
        public string Type { get; set; }
        public string Status { get; set; }
        public string? Notes { get; set; }

        // Chữ ký
        //public string? DriverSignatureUrl { get; set; }
        public DateTime? DriverSignedAt { get; set; }
        //public string? ContactSignatureUrl { get; set; }
        public DateTime? ContactSignedAt { get; set; }
        //public Guid DriverId { get; set; } // FK to Driver

        //public Guid TripContactId { get; set; } // FK to TripContact

        public bool? DriverSigned { get; set; }
        public bool? ContactSigned { get; set; }


        // Đối tượng liên quan
        public TripContactForDriverDTO? TripContact { get; set; }
        public TripDriverAssignmentForDriverDTO DriverPrimary { get; set; }
        public DeliveryRecordTemplateForDriverDTO? DeliveryRecordTemplate { get; set; }

        // THÊM MỚI: Thông tin chuyến đi và hàng hóa chi tiết
        public TripDetailForRecordForDriverDTO? TripDetail { get; set; }
    }

    // Các DTO phụ trợ cho Trip và Package
    public class TripDetailForRecordForDriverDTO
    {
        public string TripCode { get; set; }
        public string Status { get; set; }
        public string Type { get; set; }
        public List<PackageDetailForDriverDTO> Packages { get; set; } = new();
    }

    public class PackageDetailForDriverDTO
    {
        public Guid PackageId { get; set; }
        public string PackageCode { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public int Quantity { get; set; }
        public string? Unit { get; set; }
        public decimal WeightKg { get; set; }
        public decimal VolumeM3 { get; set; }
        public List<string> HandlingAttributes { get; set; }

        // Thông tin Item bên trong
        public ItemDetailForDriverDTO? Item { get; set; }
        public List<string> ImageUrls { get; set; } // Lấy từ PackageImages
    }

    public class ItemDetailForDriverDTO
    {
        public Guid ItemId { get; set; }
        public string ItemCode { get; set; }
        public string Name { get; set; }
        public List<string>? ImageUrls { get; set; } // Lấy từ ItemImages
        // Thêm các thuộc tính khác của Item nếu cần
    }
    public class TripContactForDriverDTO
    {
        public Guid TripContactId { get; set; }
        public string Type { get; set; } = "";
        public string FullName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public string? Note { get; set; }
    }

    public class DeliveryRecordTemplateForDriverDTO
    {
        public Guid DeliveryRecordTemplateId { get; set; }
        public string TemplateName { get; set; } = null!;
        public string Version { get; set; } = null!;
        public string Type { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public List<DeliveryRecordTermForDriverDTO> DeliveryRecordTerms { get; set; } = new List<DeliveryRecordTermForDriverDTO>();

    }

    public class DeliveryRecordTermForDriverDTO
    {
        public Guid DeliveryRecordTermId { get; set; }
        public Guid DeliveryRecordTemplateId { get; set; }
        public string Content { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class TripDriverAssignmentForDriverDTO
    {
        public Guid DriverId { get; set; }
        public string FullName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        //public string Type { get; set; } = "";
        //public string AssignmentStatus { get; set; } = "";
        //public string PaymentStatus { get; set; } = "";
    }
}
