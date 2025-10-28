using Common.Enums.Status;
using Common.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class BaseUser
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



        //
        public virtual Role Role { get; set; }
        public Guid RoleId { get; set; }
        //
        public virtual Wallet Wallet { get; set; }
        public Guid WalletId { get; set; }
        //
        public virtual ICollection<UserDocument> UserDocuments { get; set; }
        public virtual ICollection<UserToken> UserTokens { get; set; }
        public virtual ICollection<UserViolation> UserViolations { get; set; }
        public virtual ICollection<Transaction> Transactions { get; set; }

    }
}
