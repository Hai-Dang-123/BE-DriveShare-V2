using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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
        [HttpPost("create-provider-post-package")]
        public async Task<IActionResult> CreateProviderPostPackage([FromBody] PostPackageCreateDTO postPackageCreateDTO)
        {
            var response = await _postPackageService.CreateProviderPostPackageAsync(postPackageCreateDTO);
            return StatusCode(response.StatusCode, response);
        }
        [HttpPut("change-post-package-status")]
        public async Task<IActionResult> ChangePostPackageStatus([FromBody] ChangePostPackageStatusDTO changePostPackageStatusDTO)
        {
            var response = await _postPackageService.ChangePostPackageStatusAsync(changePostPackageStatusDTO);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("get-all")]
        public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
        {
            var result = await _postPackageService.GetAllPostPackagesAsync(pageNumber, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("get-by-provider/{providerId}")]
        public async Task<IActionResult> GetByProvider(
            [FromRoute] Guid providerId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _postPackageService.GetPostPackagesByProviderIdAsync(providerId, pageNumber, pageSize);
            return StatusCode(result.StatusCode, result);
        }
        [HttpGet("get-my-posts")]
        public async Task<IActionResult> GetMyPostPackages(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
        {
            var result = await _postPackageService.GetMyPostPackagesAsync(pageNumber, pageSize);
            return StatusCode(result.StatusCode, result);
        }
        [HttpGet("get-details/{postPackageId}")]
        public async Task<IActionResult> GetPostPackageDetails([FromRoute] Guid postPackageId)
        {
            var result = await _postPackageService.GetPostPackageDetailsAsync(postPackageId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("get-open")] // Lấy các bài đăng đang MỞ
        public async Task<IActionResult> GetOpenPostPackages(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
        {
            var result = await _postPackageService.GetOpenPostPackagesAsync(pageNumber, pageSize);
            return StatusCode(result.StatusCode, result);
        }
    }
}
