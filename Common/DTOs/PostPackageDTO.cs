using Common.Enums.Status;
using Common.ValueObjects;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class PostPackageDTO
    {
    }
    // --- DTOs/ShippingRoute/ShippingRouteInputDTO.cs ---
    // (DTO con để nhận thông tin tạo ShippingRoute)
    public class ShippingRouteInputDTO
    {
        [Required]
        public Location StartLocation { get; set; }
        [Required]
        public Location EndLocation { get; set; }
        [Required]
        public DateTime ExpectedPickupDate { get; set; }
        [Required]
        public DateTime ExpectedDeliveryDate { get; set; }
        public TimeOnly? StartTimeToPickup { get; set; }
        public TimeOnly? EndTimeToPickup { get; set; }
        public TimeOnly? StartTimeToDelivery { get; set; }
        public TimeOnly? EndTimeToDelivery { get; set; }

        // [NEW] Lưu trữ kết quả tính toán từ Vietmap
        public double? EstimatedDistanceKm { get; set; }
        public double? EstimatedDurationHours { get; set; }

        // Tách biệt rõ ràng
        public double? TravelTimeHours { get; set; }  // Thời gian chạy thực tế
        public double? WaitTimeHours { get; set; }    // Thời gian chờ (quan trọng)
        public string? RestrictionNote { get; set; }  // Ghi chú lý do cấm
    }

    // --- DTOs/PostPackage/PostContactInputDTO.cs ---
    // (DTO con để nhận thông tin liên hệ)
    public class PostContactInputDTO
    {
        [Required]
        public string FullName { get; set; } = string.Empty;
        [Required]
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Note { get; set; }
    }

    // --- DTOs/PostPackage/PostPackageCreateDTO.cs ---
    // (DTO chính đã được cập nhật)
    public class PostPackageCreateDTO
    {
        // 1. Thông tin bài đăng
        [Required]
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        [Required]
        public decimal OfferedPrice { get; set; }

        // 2. Thông tin lộ trình (Nested DTO)
        [Required]
        public ShippingRouteInputDTO ShippingRoute { get; set; }

        // 3. Thông tin liên hệ (Nested DTOs)
        [Required]
        public PostContactInputDTO SenderContact { get; set; }
        [Required]
        public PostContactInputDTO ReceiverContact { get; set; }

        [Required]
        [MinLength(1)] // Phải có ít nhất 1 Package ID
        public List<Guid> PackageIds { get; set; }

        [Required]
        public PostStatus Status { get; set; } 
    }
    public class  ChangePostPackageStatusDTO
    {
        public Guid PostPackageId { get; set; }
        public PostStatus NewStatus { get; set; }
    }

    // --- DTOs/PostPackage/PostContactReadDTO.cs ---
    // DTO rút gọn cho thông tin liên hệ
    public class PostContactReadDTO
    {
        public Guid PostContactId { get; set; }
        public string Type { get; set; } // SENDER hoặc RECEIVER
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string? Email { get; set; }
    }

    // --- DTOs/PostPackage/PostPackageReadDTO.cs ---
    // DTO chính cho việc đọc một PostPackage
    public class PostPackageReadDTO
    {
        public Guid PostPackageId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
        public decimal OfferedPrice { get; set; }
        public string Status { get; set; }

        // --- Dữ liệu liên quan (từ Include) ---

        // 1. Thông tin Provider
        public Guid ProviderId { get; set; }
        public string ProviderName { get; set; }
        public string? ProviderAvatar { get; set; }

        // 2. Thông tin Lộ trình (Giả sử ShippingRoute có các Location này)
        public Guid ShippingRouteId { get; set; }
        public Location StartLocation { get; set; } // (Giả định)
        public Location EndLocation { get; set; }   // (Giả định)

        // 3. Thông tin Liên hệ
        //public List<PostContactReadDTO> Contacts { get; set; } = new List<PostContactReadDTO>();

        public ShippingRouteInPostDTO ShippingRoute { get; set; } = null!;

        // 4. Thông tin Gói hàng (đếm số lượng)
        public int PackageCount { get; set; }
    }


    // --- DTOs/Shared/ProviderInfoDTO.cs ---
    // (DTO đơn giản cho thông tin Provider)
    public class ProviderInfoInPostDTO
    {
        public Guid ProviderId { get; set; }
        public string FullName { get; set; }
        public string? AvatarUrl { get; set; }
        public string PhoneNumber { get; set; }
    }

    // --- DTOs/Shared/ShippingRouteDTO.cs ---
    // (DTO cho ShippingRoute)
    public class ShippingRouteInPostDTO
    {
        public Guid ShippingRouteId { get; set; }
        public Location StartLocation { get; set; } = null!;
        public Location EndLocation { get; set; } = null!;
        public DateTime ExpectedPickupDate { get; set; }
        public DateTime ExpectedDeliveryDate { get; set; }
        public TimeWindow PickupTimeWindow { get; set; }
        public TimeWindow DeliveryTimeWindow { get; set; }
    }

    // --- DTOs/Item/ItemForPackageDTO.cs ---
    // (DTO cho Item lồng bên trong Package)
    public class ItemForPackageInPostDTO
    {
        public Guid ItemId { get; set; }
        public string ItemName { get; set; }
        public string? Description { get; set; }
        public decimal? DeclaredValue { get; set; }
        public string Currency { get; set; }
        public string Status { get; set; }
        public List<ItemImageReadDTO> ImageUrls { get; set; } = new List<ItemImageReadDTO>();
    }

    // --- DTOs/Package/PackageForPostDTO.cs ---
    // (DTO cho Package lồng bên trong PostPackage)
    public class PackageForPostDTO
    {
        public Guid PackageId { get; set; }
        public string PackageCode { get; set; }
        public string Title { get; set; }
        public decimal WeightKg { get; set; }
        public decimal VolumeM3 { get; set; }
        public string Status { get; set; }
        public List<PackageImageReadDTO> PackageImages { get; set; } = new List<PackageImageReadDTO>();
        public ItemForPackageInPostDTO Item { get; set; } = null!; // Lồng Item
                                                                   // [BỔ SUNG] Các thuộc tính này đang thiếu
        public bool IsFragile { get; set; }
        public bool IsLiquid { get; set; }
        public bool IsRefrigerated { get; set; }
        public bool IsFlammable { get; set; }
        public bool IsHazardous { get; set; }
        public bool IsBulky { get; set; }
        public bool IsPerishable { get; set; }
    }

    // --- DTOs/PostPackage/PostPackageDetailDTO.cs ---
    // (DTO CHÍNH CHO TÍNH NĂNG NÀY)
    public class PostPackageDetailDTO
    {
        public Guid PostPackageId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public decimal OfferedPrice { get; set; }
        public string Status { get; set; }
        public DateTime Created { get; set; }

        public DriverSuggestionDTO? DriverSuggestion { get; set; } // Thêm dòng này
        // [MỚI - SỬA LỖI] Thêm dòng này để fix lỗi 'does not contain definition for MyDrivers'
        public List<OwnerDriverStatusDTO>? MyDrivers { get; set; }

        public ProviderInfoInPostDTO Provider { get; set; } = null!;
        public ShippingRouteInPostDTO ShippingRoute { get; set; } = null!;
        public List<PostContactReadDTO> PostContacts { get; set; } = new List<PostContactReadDTO>();
        public List<PackageForPostDTO> Packages { get; set; } = new List<PackageForPostDTO>();
    }

    // [MỚI - SỬA LỖI] Định nghĩa class này để fix lỗi 'could not be found'
    public class OwnerDriverStatusDTO
    {
        public Guid DriverId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string AvatarUrl { get; set; }
        public bool IsAvailable { get; set; }
        public string StatusMessage { get; set; } // "Sẵn sàng" hoặc "Bận chuyến..."
        public string Stats { get; set; } // "Hôm nay: 2h"
    }

}
