using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly UserUtility _userUtility;
        public UserController(IUserService userService, UserUtility userUtility)
        {
            _userService = userService;
            _userUtility = userUtility;
        }
        [HttpGet("me")]
        public async Task<IActionResult> GetMyProfile()
        {
            var result = await _userService.GetMyProfileAsync();
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllUsers(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? sortField = null,
            [FromQuery] string? sortDirection = "ASC"
        )
        {
            var response = await _userService.GetAllAsync(pageNumber, pageSize, search, sortField, sortDirection);
            return StatusCode(response.StatusCode, response);
        }


        [HttpGet("{id}")]
        //[Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUserById(Guid id)
        {
            var response = await _userService.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("role/{roleName}")]
        public async Task<IActionResult> GetUsersByRole(
            string roleName,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? sortField = null,
            [FromQuery] string? sortDirection = "ASC"
        )
        {
            var response = await _userService.GetAllUserByRoleAsync(roleName, pageNumber, pageSize, search, sortField, sortDirection);
            return StatusCode(response.StatusCode, response);
        }

        // =========================================================
        // 🔹 4. UPDATE MY PROFILE (Tự cập nhật bản thân)
        // =========================================================
        [HttpPut("profile/me")]
        [Authorize]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateUserProfileDTO dto)
        {
            // 1. Lấy UserId từ Token
            var userId = _userUtility.GetUserIdFromToken();
            if (userId == Guid.Empty)
            {
                return Unauthorized(new { Message = "Invalid Token" });
            }

            // 2. Gọi Service update
            var response = await _userService.UpdateProfileAsync(userId, dto);
            return StatusCode(response.StatusCode, response);
        }

        // =========================================================
        // 🔹 5. UPDATE USER BY ID (Dành cho Admin sửa thông tin User)
        // =========================================================
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")] // Chỉ Admin được sửa người khác
        public async Task<IActionResult> UpdateUserById(Guid id, [FromBody] UpdateUserProfileDTO dto)
        {
            var response = await _userService.UpdateProfileAsync(id, dto);
            return StatusCode(response.StatusCode, response);
        }

        // =========================================================
        // 🔹 6. DELETE USER (Soft Delete)
        // =========================================================
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")] // CỰC KỲ QUAN TRỌNG: Chỉ Admin được xóa
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            var response = await _userService.DeleteUserAsync(id);
            return StatusCode(response.StatusCode, response);
        }
    }
}
