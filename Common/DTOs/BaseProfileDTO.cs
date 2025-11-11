using Common.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class BaseProfileDTO
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Status { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? AvatarUrl { get; set; }
        public bool? IsEmailVerified { get; set; }
        public bool? IsPhoneVerified { get; set; }
        public Location Address { get; set; } // Giả sử Location là an toàn để expose
        public string Role { get; set; } = null!;
    }
}
