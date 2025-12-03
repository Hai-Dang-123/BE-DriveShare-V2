using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        public AuthController (IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> LoginAsync (LoginDTO dto)
        {
            var response = await _authService.LoginAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("register-driver")]
        public async Task<IActionResult> RegisterForDriverAsync([FromForm] RegisterForDriverDTO dto)
        {
            var response = await _authService.RegisterForDriver(dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("register-owner")]
        public async Task<IActionResult> RegisterForOwnerAsync([FromForm] RegisterForOwnerDTO dto)
        {
            var response = await _authService.RegisterForOwner(dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("register-provider")]
        public async Task<IActionResult> RegisterForProviderAsync([FromForm] RegisterForProviderDTO dto)
        {
            var response = await _authService.RegisterForProvider(dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("register-for-admin")]
        public async Task<IActionResult> RegisterForAdminAsync([FromForm] RegisterForAdminDTO dto)
        {
            var response = await _authService.RegisterForAdmin(dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshTokenAsync(RefreshTokenDTO dto)
        {
            var response = await _authService.RefreshTokenAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> LogoutAsync ()
        {
            var response = await _authService.LogoutAsync();
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("verify-email")]
        public async Task<IActionResult> VerifyEmailAsync ([FromQuery] Guid userId, [FromQuery] string token)
        {
            var response = await _authService.VerifyEmailAsync(userId, token);
            return StatusCode(response.StatusCode, response);
        }
    }
}
