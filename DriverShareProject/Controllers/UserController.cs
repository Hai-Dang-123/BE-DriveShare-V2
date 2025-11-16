using BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        public UserController(IUserService userService)
        {
            _userService = userService;
        }
        [HttpGet("me")]
        public async Task<IActionResult> GetMyProfile()
        {
            var result = await _userService.GetMyProfileAsync();
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet]
        //[Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllUsers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var response = await _userService.GetAllAsync(pageNumber, pageSize);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("{id}")]
        //[Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUserById(Guid id)
        {
            var response = await _userService.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }
    }
}
