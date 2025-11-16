using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize] // 🚩 Bắt buộc tất cả API trong đây phải đăng nhập
    public class TripController : ControllerBase
    {
        private readonly ITripService _tripService;

        public TripController(ITripService tripService)
        {
            _tripService = tripService;
        }

        [HttpPost("owner-create-from-post")]
        //[Authorize(Roles = "Owner")] // 🚩 Chỉ Owner mới được gọi API này
        public async Task<IActionResult> CreateTripFromPost(
          [FromBody] TripCreateFromPostDTO dto)
        {
            var result = await _tripService.CreateTripFromPostAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("change-status")]
        public async Task<IActionResult> ChangeStatus([FromBody] ChangeTripStatusDTO dto)
        {
            // LƯU Ý: Service của bạn chưa kiểm tra quyền (ví dụ: chỉ Owner/Driver của chuyến đi mới được đổi)
            // Tạm thời [Authorize] ở class là đủ để biết user đã đăng nhập
            var result = await _tripService.ChangeTripStatusAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        // ==========================================
        // === 🚩 HÀM ĐÃ SỬA (OWNER) 🚩 ===
        // ==========================================
        [HttpGet("owner")]
        //[Authorize(Roles = "Owner")] // 🚩 Chỉ Owner
        public async Task<IActionResult> GetAllTripsByOwner(
      [FromQuery] int pageNumber = 1,
      [FromQuery] int pageSize = 10)
        {
            // Service sẽ tự lấy OwnerId từ token
            var result = await _tripService.GetAllTripsByOwnerAsync(pageNumber, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        // ==========================================
        // === 🚩 HÀM ĐÃ SỬA (DRIVER) 🚩 ===
        // ==========================================
        [HttpGet("driver")]
        //[Authorize(Roles = "Driver")] // 🚩 Chỉ Driver
        public async Task<IActionResult> GetAllTripsByDriver(
      [FromQuery] int pageNumber = 1,
      [FromQuery] int pageSize = 10)
        {
            // Service sẽ tự lấy DriverId từ token
            var response = await _tripService.GetAllTripsByDriverAsync(pageNumber, pageSize);
            return StatusCode(response.StatusCode, response);
        }

        // ⚠️ THÊM MỚI TẠI ĐÂY
        [HttpGet("provider")]
        //[Authorize(Roles = "Provider")] // Đảm bảo chỉ Provider mới gọi được
        public async Task<IActionResult> GetAllTripsByProvider([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var response = await _tripService.GetAllTripsByProviderAsync(pageNumber, pageSize);
            return StatusCode(response.StatusCode, response);
        }

        // ==========================================
        // === 🚩 HÀM ĐÃ SỬA (GetById) 🚩 ===
        // ==========================================
        [HttpGet("{tripId}")]
        // (Không cần Role, vì Service sẽ kiểm tra (Owner CỦA CHUYẾN ĐI hoặc Driver ĐƯỢC GÁN))
        public async Task<IActionResult> GetTripById(Guid tripId)
        {
            var response = await _tripService.GetTripByIdAsync(tripId);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("all")]
        //[Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllTrips([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var response = await _tripService.GetAllAsync(pageNumber, pageSize);
            return StatusCode(response.StatusCode, response);
        }
    }
}