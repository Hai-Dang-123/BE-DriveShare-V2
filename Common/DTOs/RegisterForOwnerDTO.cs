using Common.Enums.Status;
using Common.ValueObjects;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace Common.DTOs
{
    public class RegisterForOwnerDTO
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Password { get; set; }
        public string ConfirmPassword { get; set; }
        public IFormFile? AvatarFile { get; set; }
        public string Address { get; set; }
        public DateTime DateOfBirth { get; set; }

        // 

        public string? CompanyName { get; set; }
        public string? TaxCode { get; set; } = null!;
        public string? BussinessAddress { get; set; }



    }

      

     
}
