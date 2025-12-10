using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TripProviderContractController : ControllerBase
    {
        private readonly ITripProviderContractService _tripProviderContractService;

        public TripProviderContractController(ITripProviderContractService tripProviderContractService)
        {
            _tripProviderContractService = tripProviderContractService;
        }
        [HttpPost("create")]
        public async Task<IActionResult> CreateContract([FromBody] CreateTripProviderContractDTO dto)
        {
            var result = await _tripProviderContractService.CreateAsync(dto);
            return StatusCode(result.StatusCode, result);
        }
        [HttpPut("sign")]
        //[Authorize] // Bắt buộc có Token
        public async Task<IActionResult> SignContract([FromBody] SignContractDTO dto)
        {
            // Gọi service với mã OTP user gửi lên
            var response = await _tripProviderContractService.SignAsync(dto);

            return StatusCode(response.StatusCode, response);
        }
       

        /// <summary>
        /// [Owner/Provider] Lấy danh sách Hợp đồng Nhà cung cấp (có phân trang).
        /// </summary>
        [HttpGet("provider-contracts")]
        public async Task<IActionResult> GetAllProviderContracts([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var response = await _tripProviderContractService.GetAllAsync(pageNumber, pageSize);
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// [Owner/Provider] Lấy chi tiết Hợp đồng Nhà cung cấp.
        /// </summary>
        [HttpGet("provider-contracts/{id}")]
        public async Task<IActionResult> GetProviderContractById(Guid id)
        {
            var response = await _tripProviderContractService.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("send-sign-otp")]
        public async Task<IActionResult> SendSignOtp([FromBody] SendSignOtpDTO dto) // Hoặc tạo DTO { ContractId: Guid }
        {
            var response = await _tripProviderContractService.SendOTPToSignContract(dto.ContractId);
            return StatusCode(response.StatusCode, response);
        }

        public class SendSignOtpDTO
        {
            public Guid ContractId { get; set; }
        }
    }
}
