using BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize] // Bắt buộc đăng nhập mới lấy được Token (Bảo mật)
    public class VNPTController : ControllerBase
    {
        private readonly IEKYCService _ekycService;

        public VNPTController(IEKYCService ekycService)
        {
            _ekycService = ekycService;
        }

        /// <summary>
        /// Lấy AccessToken, TokenID, TokenKey để khởi tạo VNPT SDK ở Frontend
        /// </summary>
        [HttpGet("get-config")]
        public async Task<IActionResult> GetConfig()
        {
            var result = await _ekycService.GetVnptSdkConfigAsync();
            return StatusCode(result.StatusCode, result);
        }
    }
}
