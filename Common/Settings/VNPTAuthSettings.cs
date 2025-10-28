using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Settings
{
    public class VNPTAuthSettings
    {
        public string BaseUrl { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string ClientId { get; set; } = null!;
        public string GrantType { get; set; } = null!;
        public string ClientSecret { get; set; } = null!;
    }
}
