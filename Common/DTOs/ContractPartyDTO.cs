using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    // 1. DTO chứa thông tin chi tiết của một bên (Party)
    public class ContractPartyDTO
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; }      // Tên người đại diện
        public string CompanyName { get; set; }   // Tên công ty (Quan trọng cho Owner/Provider)
        public string TaxCode { get; set; }       // Mã số thuế (Quan trọng cho Owner/Provider)
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }       // Địa chỉ kinh doanh hoặc thường trú
        public string Role { get; set; }          // "Owner", "Provider", "Driver"
        public string LicenseNumber { get; set; } // (Dành riêng cho Driver nếu cần)
    }
}
