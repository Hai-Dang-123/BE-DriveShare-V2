using BLL.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostAnalysisController : ControllerBase
    {
        private readonly IPostAnalysisService _postAnalysisService;
        public PostAnalysisController(IPostAnalysisService postAnalysisService)
        {
            _postAnalysisService = postAnalysisService;
        }

        // Thêm /{postPackageId} vào trong ngoặc kép
        [HttpPost("analyze-post-package/{postPackageId}")]
        public async Task<IActionResult> AnalyzePostPackage([FromRoute] Guid postPackageId)
        {
            var result = await _postAnalysisService.GetOrGeneratePackageAnalysisAsync(postPackageId);
            return StatusCode(result.StatusCode, result);
        }
        [HttpPost("analyze-post-trip /{postTripId}")]
        public async Task<IActionResult> AnalyzePostTrip([FromRoute] Guid postTripId)
        {
            var result = await _postAnalysisService.GetOrGenerateTripAnalysisAsync(postTripId);
            return StatusCode(result.StatusCode, result);
        }
    }

}
