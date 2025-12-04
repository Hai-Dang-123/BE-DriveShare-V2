using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Bắt buộc có Token cho tất cả các API trong Controller này
    public class DriverWorkSessionController : ControllerBase
    {
        private readonly IDriverWorkSessionService _service;

        public DriverWorkSessionController(IDriverWorkSessionService service)
        {
            _service = service;
        }

        /// <summary>
        /// Bắt đầu phiên lái xe (Start). Có kiểm tra xung đột tài xế.
        /// </summary>
        [HttpPost("start")]
        public async Task<IActionResult> StartSession([FromBody] StartSessionDTO dto)
        {
            var response = await _service.StartSessionAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// Kết thúc phiên lái xe (End).
        /// </summary>
        [HttpPut("end")]
        public async Task<IActionResult> EndSession([FromBody] EndSessionDTO dto)
        {
            var response = await _service.EndSessionAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// Kiểm tra xem tài xế có đủ điều kiện lái xe không (Luật 10h/48h).
        /// </summary>
        [HttpGet("check-eligibility")]
        public async Task<IActionResult> CheckEligibility()
        {
            var response = await _service.CheckDriverEligibilityAsync();
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// Lấy lịch sử lái xe của tài xế.
        /// </summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory([FromQuery] DriverHistoryFilterDTO filter)
        {
            var response = await _service.GetDriverHistoryAsync(filter);
            return StatusCode(response.StatusCode, response);
        }

        // =========================================================================
        // [MỚI] API LẤY THÔNG TIN SESSION HIỆN TẠI CỦA CHUYẾN ĐI
        // =========================================================================
        /// <summary>
        /// Kiểm tra xem chuyến đi này hiện tại có ai đang lái không.
        /// Dùng để hiển thị: "Đang lái bởi..." hoặc nút "Bắt đầu".
        /// </summary>
        /// <param name="tripId">ID của chuyến đi</param>
        [HttpGet("current-session/{tripId:guid}")]
        public async Task<IActionResult> GetCurrentSessionInTrip(Guid tripId)
        {
            var response = await _service.GetCurrentSessionInTripAsync(tripId);
            return StatusCode(response.StatusCode, response);
        }
    }
}