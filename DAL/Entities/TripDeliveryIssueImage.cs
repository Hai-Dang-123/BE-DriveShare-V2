using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class TripDeliveryIssueImage
    {
        public Guid TripDeliveryIssueImageId { get; set; }
        public string ImageUrl { get; set; } = null!; // Link ảnh (bắt buộc)
        public string? Caption { get; set; } // Chú thích cho ảnh
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 
        public virtual TripDeliveryIssue TripDeliveryIssue { get; set; } = null!;
        public Guid TripDeliveryIssueId { get; set; }
    }
}
