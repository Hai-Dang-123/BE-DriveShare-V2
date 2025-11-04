using BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TripDeliveryIssueController : ControllerBase
    {
        private readonly ITripDeliveryIssueService _tripDeliveryIssueService;
        public TripDeliveryIssueController(ITripDeliveryIssueService tripDeliveryIssueService)
        {
            _tripDeliveryIssueService = tripDeliveryIssueService;
        }
        [HttpPost("create")]
        public async Task<IActionResult> CreateTripDeliveryIssue([FromForm] Common.DTOs.TripDeliveryIssueCreateDTO dto)
        {
            var result = await _tripDeliveryIssueService.CreateTripDeliveryIssue(dto);
            return StatusCode(result.StatusCode, result);
        }
    }
    }
