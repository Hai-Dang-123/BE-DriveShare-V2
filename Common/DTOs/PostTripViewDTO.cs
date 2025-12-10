using Common.Enums.Status;
using Common.Enums.Type;
using System;
using System.Collections.Generic;

namespace Common.DTOs
{
    public class PostTripViewDTO
    {
        public Guid PostTripId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public PostStatus Status { get; set; }
        public DateTime CreateAt { get; set; }
        public DateTime UpdateAt { get; set; }
        public decimal? RequiredPayloadInKg { get; set; }

        // [XÓA BỎ] 
        // public DriverType Type { get; set; } // (Trường này không còn tồn tại trên PostTrip)

        // Thông tin Owner (đơn giản)
        public OwnerSimpleDTO? Owner { get; set; }

        // [SỬA ĐỔI] - Hiển thị thông tin Trip chi tiết hơn
        public TripSummaryForPostDTO? Trip { get; set; }

        // Thông tin chi tiết các slot tuyển dụng
        public virtual ICollection<PostTripDetailViewDTO> PostTripDetails { get; set; } = new List<PostTripDetailViewDTO>();
    }

    public class PostTripDetailViewDTO
    {
        public Guid PostTripDetailId { get; set; }
        public DriverType Type { get; set; } // Chính / Phụ (Nằm ở đây là đúng)
        public int RequiredCount { get; set; }
        public decimal PricePerPerson { get; set; }
        public decimal? TotalBudget { get; set; }
        public string PickupLocation { get; set; } = string.Empty;
        public string DropoffLocation { get; set; } = string.Empty;
        public bool MustPickAtGarage { get; set; }
        public bool MustDropAtGarage { get; set; }

        public decimal DepositAmount { get; set; }

    }

    // --- DTOs đơn giản cho các quan hệ ---

    public class OwnerSimpleDTO
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
    }

    // [SỬA ĐỔI] - DTO này được làm giàu thông tin hơn
    public class TripSummaryForPostDTO
    {
        public Guid TripId { get; set; }
        public string StartLocationName { get; set; } = string.Empty;
        public string EndLocationName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate  { get; set; }

        // --- THÔNG TIN BỔ SUNG "ĐỦ" CHO DRIVER ---

        // Thông tin xe (Lấy từ Trip.Vehicle)
        public string? VehicleModel { get; set; }
        public string? VehiclePlate { get; set; }
        public string? VehicleType { get; set; } // Loại xe (thùng kín, xe lạnh...)

        // Thông tin hàng hóa (Lấy từ Trip.Packages)
        public int PackageCount { get; set; } = 0;
        public string? TripDescription { get; set; } // Mô tả chung của chuyến đi (nếu có)
    }
}