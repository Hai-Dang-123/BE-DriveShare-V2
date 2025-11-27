using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DriverWorkSessionController : ControllerBase
    {
        private readonly IDriverWorkSessionService _service;

        public DriverWorkSessionController(IDriverWorkSessionService service)
        {
            _service = service;
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartSession([FromBody] StartSessionDTO dto)
        {
            var response = await _service.StartSessionAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("end")]
        public async Task<IActionResult> EndSession([FromBody] EndSessionDTO dto)
        {
            var response = await _service.EndSessionAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // Không cần tham số driverId trên URL nữa
        [HttpGet("check-eligibility")]
        public async Task<IActionResult> CheckEligibility()
        {
            var response = await _service.CheckDriverEligibilityAsync();
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory([FromQuery] DriverHistoryFilterDTO filter)
        {
            // Nếu client không gửi PageIndex/PageSize, DTO sẽ tự lấy mặc định (1, 10)
            var response = await _service.GetDriverHistoryAsync(filter);

            return StatusCode(response.StatusCode, response);
        }
    }
}
