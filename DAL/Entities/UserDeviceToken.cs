using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    
    public class UserDeviceToken
    {

        public Guid UserDeviceTokenId { get; set; }

        public Guid UserId { get; set; } // Khóa ngoại trỏ về User

        public string DeviceToken { get; set; } = null!; // Token FCM (Dài, nên để string max)


        public string Platform { get; set; } = null!; // "android", "ios", "web"

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        public virtual BaseUser User { get; set; } = null!;
    }
}
