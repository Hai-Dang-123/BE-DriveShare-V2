using BLL.Services.Interface;
using Common.DTOs;
using Common.DTOs.TripVehicleHandoverRecord;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // nếu bạn có auth
    public class TripVehicleHandoverRecordController : ControllerBase
    {
        private readonly ITripVehicleHandoverRecordService _handoverService;

        public TripVehicleHandoverRecordController(ITripVehicleHandoverRecordService handoverService)
        {
            _handoverService = handoverService;
        }

        // ============================================================================
        // 1. CREATE new handover (PICKUP / DROPOFF)
        // ============================================================================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TripVehicleHandoverRecordCreateDTO dto)
        {
            var result = await _handoverService.CreateTripVehicleHandoverRecordAsync(dto);

            if (result) return Ok(new { Success = true, Message = "Tạo biên bản giao nhận xe thành công." });

            return BadRequest(new { Success = false, Message = "Tạo biên bản thất bại." });
        }

        // ============================================================================
        // 2. GET BY ID
        // ============================================================================
        [HttpGet("{recordId}")]
        public async Task<IActionResult> GetById(Guid recordId)
        {
            var response = await _handoverService.GetByIdAsync(recordId);
            return StatusCode(response.StatusCode, response);
        }

        // ============================================================================
        // 3. SEND OTP
        // ============================================================================
        [HttpPost("{recordId}/send-otp")]
        public async Task<IActionResult> SendOtp(Guid recordId)
        {
            var response = await _handoverService.SendOtpAsync(recordId);
            return StatusCode(response.StatusCode, response);
        }

        // ============================================================================
        // 4. SIGN RECORD
        // ============================================================================
        [HttpPost("sign")]
        public async Task<IActionResult> SignRecord([FromBody] SignVehicleHandoverDTO dto)
        {
            var response = await _handoverService.SignRecordAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // ============================================================================
        // 5. UPDATE CHECKLIST
        // ============================================================================
        [HttpPut("update-checklist")]
        public async Task<IActionResult> UpdateChecklist([FromForm] UpdateHandoverChecklistDTO dto)
        {
            var response = await _handoverService.UpdateChecklistAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // ============================================================================
        // 6. REPORT ISSUE
        // ============================================================================
        [HttpPost("report-issue")]
        public async Task<IActionResult> ReportIssue([FromForm] ReportHandoverIssueDTO dto)
        {
            var response = await _handoverService.ReportIssueAsync(dto);
            return StatusCode(response.StatusCode, response);
        }
    }
}
