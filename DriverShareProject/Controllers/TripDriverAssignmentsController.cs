using BLL.Services.Impletement;
using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TripDriverAssignmentsController : ControllerBase
    {
        private readonly ITripDriverAssignmentService _assignmentService;

        public TripDriverAssignmentsController(ITripDriverAssignmentService assignmentService)
        {
            _assignmentService = assignmentService;
        }

        /// <summary>
        /// (Owner) Gán một tài xế (nội bộ hoặc thuê ngoài) vào một chuyến đi.
        /// </summary>
        [HttpPost("assign-driver-by-owner")] // Đổi tên để rõ ràng
        //[Authorize(Roles = "Owner")]
        public async Task<IActionResult> CreateAssignmentByOwner([FromBody] CreateAssignmentDTO dto)
        {
            var response = await _assignmentService.CreateAssignmentByOwnerAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // ⚠️ ENDPOINT MỚI:
        /// <summary>
        /// (Driver) Ứng tuyển (apply/bid) vào một PostTrip (Bài đăng tìm tài xế).
        /// </summary>
        [HttpPost("apply-post-trip")]
        //[Authorize(Roles = "Driver")]
        public async Task<IActionResult> CreateAssignmentByPostTrip([FromBody] CreateAssignmentByPostTripDTO dto)
        {
            var response = await _assignmentService.CreateAssignmentByPostTripAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("check-in")]
        
        public async Task<IActionResult> CheckIn([FromForm] DriverCheckInDTO dto)
        {
            var result = await _assignmentService.DriverCheckInAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("check-out")]

        public async Task<IActionResult> CheckOut([FromForm] DriverCheckOutDTO dto)
        {
            var result = await _assignmentService.DriverCheckOutAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        // 5. Owner/Admin xóa tài xế khỏi chuyến (Có hoàn cọc)
        [HttpDelete("{assignmentId}")]
        //[Authorize(Roles = "Owner,Admin")]
        public async Task<IActionResult> RemoveDriverFromTrip(Guid assignmentId)
        {
            var result = await _assignmentService.CancelAssignmentAsync(assignmentId);
            return StatusCode(result.StatusCode, result);
        }

        // 6. Tài xế tự hủy chuyến (Có check quy định 3 ngày & hoàn cọc)
        [HttpDelete("self-cancel/{assignmentId}")]
        //[Authorize(Roles = "Driver")]
        public async Task<IActionResult> DriverSelfCancel(Guid assignmentId)
        {
            var result = await _assignmentService.CancelAssignmentByDriverAsync(assignmentId);
            return StatusCode(result.StatusCode, result);
        }
    }
}
