using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class VehicleDocumentResponseDTO
    {
        public Guid VehicleDocumentId { get; set; }
        public string DocumentType { get; set; }
        public string FrontImage { get; set; }
        public string? BackImage { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string Status { get; set; }

        // QUAN TRỌNG: Không để property "Vehicle" ở đây
        // Chỉ để VehicleId nếu cần
        public Guid VehicleId { get; set; }
    }
}
