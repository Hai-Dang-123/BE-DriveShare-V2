using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{

    
        public class TripVehicleHandoverTermResult
        {
            public Guid TripVehicleHandoverTermResultId { get; set; }

            // Thuộc về biên bản nào?
            public Guid TripVehicleHandoverRecordId { get; set; } // FK

            // Ứng với câu hỏi/điều khoản mẫu nào?
            public Guid DeliveryRecordTermId { get; set; } // FK to DeliveryRecordTerm

            // --- KẾT QUẢ ---
            public bool IsPassed { get; set; } // True: OK, False: Có vấn đề
            public string? Note { get; set; } // Ghi chú (vd: "Hơi mòn nhưng vẫn dùng được")

            // Ảnh minh chứng cho mục này (nếu cần)
            public string? EvidenceImageUrl { get; set; }

            // --- NAV ---
            public virtual TripVehicleHandoverRecord TripVehicleHandoverRecord { get; set; } = null!;
            public virtual DeliveryRecordTerm DeliveryRecordTerm { get; set; } = null!;
        }
    
}
