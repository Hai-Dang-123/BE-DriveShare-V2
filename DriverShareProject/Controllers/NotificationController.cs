using BLL.Services.Impletement;
using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly UserUtility _userUtility; // Utility lấy UserId từ Token JWT

        public NotificationController(INotificationService notificationService, UserUtility userUtility)
        {
            _notificationService = notificationService;
            _userUtility = userUtility;
        }

        // POST: api/Notification/register-token
        [HttpPost("register-token")]
        public async Task<IActionResult> RegisterToken([FromBody] RegisterTokenRequest request)
        {
            var userId = _userUtility.GetUserIdFromToken();
            if (userId == Guid.Empty) return Unauthorized(new ResponseDTO("Unauthorized", 401, false));

            var result = await _notificationService.RegisterDeviceTokenAsync(userId, request.DeviceToken, request.Platform);

            if (result) return Ok(new ResponseDTO("Token registered successfully", 200, true));
            return BadRequest(new ResponseDTO("Failed to register token", 400, false));
        }

        // GET: api/Notification/my-notifications?pageNumber=1&pageSize=20
        [HttpGet("my-notifications")]
        //[Authorize]
        public async Task<IActionResult> GetMyNotifications([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            var userId = _userUtility.GetUserIdFromToken();
            if (userId == Guid.Empty) return Unauthorized(new ResponseDTO("Unauthorized", 401, false));

            var result = await _notificationService.GetMyNotificationsAsync(userId, pageNumber, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        // GET: api/Notification/unread-count
        [HttpGet("unread-count")]
        //[Authorize]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = _userUtility.GetUserIdFromToken();
            if (userId == Guid.Empty) return Unauthorized(new ResponseDTO("Unauthorized", 401, false));

            var result = await _notificationService.GetUnreadCountAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        // PUT: api/Notification/{id}/mark-read
        [HttpPut("{id}/mark-read")]
        //[Authorize]
        public async Task<IActionResult> MarkRead(Guid id)
        {
            var userId = _userUtility.GetUserIdFromToken();
            if (userId == Guid.Empty) return Unauthorized(new ResponseDTO("Unauthorized", 401, false));

            var result = await _notificationService.MarkAsReadAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        // PUT: api/Notification/mark-all-read
        [HttpPut("mark-all-read")]
        //[Authorize]
        public async Task<IActionResult> MarkAllRead()
        {
            var userId = _userUtility.GetUserIdFromToken();
            if (userId == Guid.Empty) return Unauthorized(new ResponseDTO("Unauthorized", 401, false));

            var result = await _notificationService.MarkAllAsReadAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        // DELETE: api/Notification/{id}
        [HttpDelete("{id}")]
        //[Authorize]
        public async Task<IActionResult> DeleteNotification(Guid id)
        {
            var userId = _userUtility.GetUserIdFromToken();
            if (userId == Guid.Empty) return Unauthorized(new ResponseDTO("Unauthorized", 401, false));

            var result = await _notificationService.DeleteNotificationAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }
    }

    // DTO class
    public class RegisterTokenRequest
    {
        [Required]
        public string DeviceToken { get; set; } = null!;

        [Required]
        public string Platform { get; set; } = null!; // "android", "ios", "web"
    }
}
