using BLL.Services.Interface;
using Common.DTOs;
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
    }
}
