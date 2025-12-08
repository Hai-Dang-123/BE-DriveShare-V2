using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostPackageController : ControllerBase
    {
        private readonly IPostPackageService _postPackageService;

        public PostPackageController(IPostPackageService postPackageService)
        {
            _postPackageService = postPackageService;
        }

        // --- 1. TÍNH TOÁN LỘ TRÌNH & GỢI Ý THỜI GIAN (NEW) ---
        // Dùng POST để gửi body JSON chứa StartLocation/EndLocation
        [HttpPost("calculate-route")]
        public async Task<IActionResult> CalculateRoute([FromBody] RouteCalculationRequestDTO dto)
        {
            // Hàm này không lưu DB, chỉ tính toán trả về kết quả
            var response = await _postPackageService.CalculateAndValidateRouteAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // --- 2. TẠO BÀI ĐĂNG (Đã tích hợp Validate bên trong Service) ---
        [HttpPost("create-provider-post-package")]
        public async Task<IActionResult> CreateProviderPostPackage([FromBody] PostPackageCreateDTO dto)
        {
            var response = await _postPackageService.CreateProviderPostPackageAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("change-post-package-status")]
        public async Task<IActionResult> ChangePostPackageStatus([FromBody] ChangePostPackageStatusDTO dto)
        {
            var response = await _postPackageService.ChangePostPackageStatusAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("get-all")]
        public async Task<IActionResult> GetAll(
             [FromQuery] int pageNumber = 1,
             [FromQuery] int pageSize = 10,
             [FromQuery] string? search = null,
             [FromQuery] string? sortBy = null,
             [FromQuery] string? sortOrder = "ASC"
         )
        {
            var result = await _postPackageService.GetAllPostPackagesAsync(pageNumber, pageSize, search, sortBy, sortOrder);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("get-by-provider/{providerId}")]
        public async Task<IActionResult> GetByProvider(
            [FromRoute] Guid providerId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = "ASC")
        {
            var result = await _postPackageService.GetPostPackagesByProviderIdAsync(providerId, pageNumber, pageSize, search, sortBy, sortOrder);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("get-my-posts")]
        public async Task<IActionResult> GetMyPostPackages(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = "ASC")
        {
            var result = await _postPackageService.GetMyPostPackagesAsync(pageNumber, pageSize, search, sortBy, sortOrder);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("get-open")]
        public async Task<IActionResult> GetOpenPostPackages(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = "ASC")
        {
            var result = await _postPackageService.GetOpenPostPackagesAsync(pageNumber, pageSize, search, sortBy, sortOrder);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("get-details/{postPackageId}")]
        public async Task<IActionResult> GetPostPackageDetails([FromRoute] Guid postPackageId)
        {
            var result = await _postPackageService.GetPostPackageDetailsAsync(postPackageId);
            return StatusCode(result.StatusCode, result);
        }
    }
}