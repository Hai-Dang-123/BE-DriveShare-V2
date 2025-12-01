using Common.Enums.Status;
using Common.Enums.Type;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    //public class TripDeliveryIssueDTO
    //{
    //}
    // DTO để tạo mới sự cố
    public class TripDeliveryIssueCreateDTO
    {
        public Guid TripId { get; set; }
        public Guid? DeliveryRecordId { get; set; } // Nullable nếu báo cáo chung cho Trip

        public DeliveryIssueType IssueType { get; set; } // DAMAGED, LOST...
        public string Description { get; set; }
        public List<string> ImageUrls { get; set; } = new List<string>();
    }

    // DTO để đọc dữ liệu ra (Read)
    public class TripDeliveryIssueReadDTO
    {
        public Guid TripDeliveryIssueId { get; set; }
        public Guid TripId { get; set; }
        public Guid? DeliveryRecordId { get; set; }

        // Người báo cáo
        public Guid ReportedByUserId { get; set; }
        public string ReportedByUserName { get; set; }

        public string IssueType { get; set; } // Trả về string cho FE dễ hiển thị
        public string Description { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<string> ImageUrls { get; set; } = new List<string>();

        // Danh sách các khoản phạt liên quan (nếu có)
        public List<IssueSurchargeDTO> Surcharges { get; set; } = new List<IssueSurchargeDTO>();
    }

    public class IssueSurchargeDTO
    {
        public Guid TripSurchargeId { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
    }

    // DTO cập nhật trạng thái
    public class UpdateIssueStatusDTO
    {
        public Guid TripDeliveryIssueId { get; set; }
        public IssueStatus NewStatus { get; set; }
    }
}
