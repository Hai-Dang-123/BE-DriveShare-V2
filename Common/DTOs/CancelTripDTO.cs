using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class CancelTripDTO
    {
        public Guid TripId { get; set; }
        public string? Reason { get; set; } // Lý do hủy (Xe hỏng, kẹt lịch...)
    }
}
