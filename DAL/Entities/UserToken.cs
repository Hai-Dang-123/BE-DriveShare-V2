using Common.Enums;
using Common.Enums.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class UserToken
    {
        public Guid UserTokenId { get; set; }
        public TokenType TokenType { get; set; }
        public string TokenValue { get; set; } = null!;
        public bool IsRevoked { get; set; }
        public DateTime ExpiredAt { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual BaseUser User { get; set; } = null!;
        public Guid UserId { get; set; }
    }
}
