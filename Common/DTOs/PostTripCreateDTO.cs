using Common.Enums.Type;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class PostTripCreateDTO
    {
        [Required]
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        [Required]
        public Guid TripId { get; set; }

        // Thông tin này nằm trên PostTrip chính
        //public DriverType Type { get; set; } = DriverType.PRIMARY;
        public decimal? RequiredPayloadInKg { get; set; }

        // Danh sách các yêu cầu chi tiết (cho tài xế chính, phụ)
        [Required]
        [MinLength(1)]
        public List<PostTripDetailCreateDTO> PostTripDetails { get; set; } = new List<PostTripDetailCreateDTO>();
    }
}
