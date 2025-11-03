using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class RegisterForAdminDTO
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Password { get; set; }
        public string ConfirmPassword { get; set; }
        public IFormFile? AvatarFile { get; set; }
        public string Address { get; set; }
        public string RoleName { get; set; }
        public DateTime DateOfBirth { get; set; }
    }
}
