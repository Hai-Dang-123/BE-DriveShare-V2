using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TripDeliveryIssueImageController : ControllerBase
    {
        private readonly ITripDeliveryIssueImageService _tripDeliveryIssueImageService;
        public TripDeliveryIssueImageController(ITripDeliveryIssueImageService tripDeliveryIssueImageService)
        {
            _tripDeliveryIssueImageService = tripDeliveryIssueImageService;
        }
        [HttpPost("create-tripdelivery-image")]
        public async Task<IActionResult> CreateTripDeliveryIssueImage([FromForm] TripDeliveryIssueImageCreateDTO dto)
        {
            var result = await _tripDeliveryIssueImageService.CreateTripDeliveryIssueImage(dto);
            return StatusCode(result.StatusCode, result);
        }
    }
}
