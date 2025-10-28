using Common.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class TripDeliveryRecord : DeliveryRecord
    {
        // DeliveryRecordId được kế thừa từ lớp cha

        // --- GỢI Ý BẮT BUỘC (Khóa ngoại - Dựa trên sơ đồ) ---

        // 1. Biên bản này của Chuyến đi nào? (Trip - TripDeliveryRecord 1-n)
        public Guid TripId { get; set; } // FK to Trip

        // 2. Driver nào ký? (Driver - TripDeliveryRecord 1-n)
        public Guid DriverId { get; set; } // FK to Driver

        // 3. Người Gửi/Nhận (Contact) nào ký?
        public Guid TripContactId { get; set; } // FK to TripContact

        // --- GỢI Ý (Nghiệp vụ Ký) ---
        public string? DriverSignatureUrl { get; set; } // Ảnh chữ ký của Driver
        public DateTime? DriverSignedAt { get; set; }
        public string? ContactSignatureUrl { get; set; } // Ảnh chữ ký của Người gửi/nhận
        public DateTime? ContactSignedAt { get; set; }
        //public Location? SignLocation { get; set; } // Vị trí ký (nếu cần)

        // --- GỢI Ý BẮT BUỘC (Thuộc tính điều hướng) ---

        // Liên kết n-1
        public virtual Trip Trip { get; set; } = null!;
        public virtual Driver Driver { get; set; } = null!;
        public virtual TripContact TripContact { get; set; } = null!;

        // Liên kết 1-n (Một biên bản có thể có nhiều Vấn đề phát sinh)
        public virtual ICollection<TripDeliveryIssue> Issues { get; set; } = new List<TripDeliveryIssue>();

    }
}
