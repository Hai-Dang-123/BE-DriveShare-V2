using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class SignContractDTO
    {

        [Required]
        public Guid ContractId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã OTP")]
        public string Otp { get; set; } = string.Empty;
    }
}
