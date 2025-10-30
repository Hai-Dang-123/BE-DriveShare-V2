using Common.Enums.Status;
using Common.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class GetDetailOwnerDTO
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdatedAt { get; set; }
        public UserStatus Status { get; set; } = UserStatus.INACTIVE;
        public DateTime? DateOfBirth { get; set; }
        public string? AvatarUrl { get; set; }
        public bool? IsEmailVerified { get; set; }
        public bool? IsPhoneVerified { get; set; }
        public Location Address { get; set; } = null!;

        public string? CompanyName { get; set; } = null!;
        public string? TaxCode { get; set; } = null!;
        public Location? BusinessAddress { get; set; } = null!;
        public decimal? AverageRating { get; set; } = null!;
    }
}
