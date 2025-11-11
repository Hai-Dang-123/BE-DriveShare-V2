using BLL.Services.Interface;
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
    }
}
