using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TripDeliveryRecordController : ControllerBase
    {
        private readonly ITripDeliveryRecordService _tripDeliveryRecordService;
        public TripDeliveryRecordController(ITripDeliveryRecordService tripDeliveryRecordService)
        {
            _tripDeliveryRecordService = tripDeliveryRecordService;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateTripDeliveryRecord([FromBody] TripDeliveryRecordCreateDTO dto)
        {
            var result = await _tripDeliveryRecordService.CreateTripDeliveryRecordAsync(dto);
            return StatusCode(result.StatusCode, result);
        }
        [HttpGet("Get-TripDelivery-by-TripId")]
        public async Task<IActionResult> GetTripDeliveryRecordsByTripId([FromQuery] Guid tripId)
        {
            var result = await _tripDeliveryRecordService.GetByTripIdAsync(tripId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("Get-TripDelivery-by-Id")]
        public async Task<IActionResult> GetTripDeliveryRecordById([FromQuery] Guid tripDeliveryRecordId)
        {
            var result = await _tripDeliveryRecordService.GetByIdAsync(tripDeliveryRecordId);
            return StatusCode(result.StatusCode, result);
        }
        /// <summary>
        /// Bước 1: Gửi OTP xác thực ký biên bản (Gửi qua Email)
        /// </summary>
        /// <param name="recordId">ID của biên bản cần ký</param>
        [HttpPost("send-sign-otp/{recordId}")]
        public async Task<IActionResult> SendSignOtp(Guid recordId)
        {
            // Bạn cần implement hàm này trong Service (xem code bổ sung bên dưới)
            var response = await _tripDeliveryRecordService.SendOTPToSignDeliveryRecordAsync(recordId);
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// Bước 2: Ký biên bản (Xác thực OTP và cập nhật trạng thái)
        /// </summary>
        /// <param name="dto">Chứa RecordId và mã OTP</param>
        [HttpPut("sign")]
        public async Task<IActionResult> SignDeliveryRecord([FromBody] SignDeliveryRecordDTO dto)
        {
            var response = await _tripDeliveryRecordService.SignDeliveryRecordAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// [User] Lấy tất cả biên bản liên quan đến user (có phân trang).
        /// (Admin thấy tất cả, Owner/Driver thấy của mình)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllRecords([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var response = await _tripDeliveryRecordService.GetAllAsync(pageNumber, pageSize);
            return StatusCode(response.StatusCode, response);
        }


        [HttpGet("driver/{id}")]
        public async Task<IActionResult> GetByIdForDriver(Guid id)
        {
            

            var response = await _tripDeliveryRecordService.GetByIdForDriverAsync(id);

            return StatusCode(response.StatusCode, response);
        }



        /// <summary>
        /// 1. Xem chi tiết biên bản (Dành cho người nhận link qua email)
        /// </summary>
        /// <param name="recordId">ID biên bản</param>
        /// <param name="accessToken">Token bảo mật lấy từ URL</param>
        [HttpGet("contact/view/{recordId}")]
        [AllowAnonymous] // 🔓 Mở công khai
        public async Task<IActionResult> GetForContact(Guid recordId, [FromQuery] string accessToken)
        {
            if (string.IsNullOrEmpty(accessToken))
                return BadRequest("Access token is required.");

            var response = await _tripDeliveryRecordService.GetDeliveryRecordForContactAsync(recordId, accessToken);
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// 2. Yêu cầu gửi OTP để ký (Dành cho Contact)
        /// </summary>
        [HttpPost("contact/send-otp/{recordId}")]
        [AllowAnonymous] // 🔓 Mở công khai
        public async Task<IActionResult> SendOtpToContact(Guid recordId, [FromBody] ContactRequestOtpDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Gọi service gửi OTP (check quyền bằng accessToken trong dto)
            var response = await _tripDeliveryRecordService.SendOTPToContactAsync(recordId, dto.AccessToken);
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// 3. Xác nhận ký biên bản (Dành cho Contact)
        /// </summary>
        [HttpPost("contact/sign")]
        [AllowAnonymous] // 🔓 Mở công khai
        public async Task<IActionResult> SignForContact([FromBody] ContactSignDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var response = await _tripDeliveryRecordService.SignDeliveryRecordForContactAsync(dto.DeliveryRecordId, dto.Otp, dto.AccessToken);
            return StatusCode(response.StatusCode, response);
        }

        public class ContactRequestOtpDTO
        {
            [Required]
            public string AccessToken { get; set; } = string.Empty;
        }

        // DTO dùng để Ký (Kế thừa từ DTO ký của Driver + thêm AccessToken)
        public class ContactSignDTO : SignDeliveryRecordDTO
        {
            [Required]
            public string AccessToken { get; set; } = string.Empty;
        }


    }
}
