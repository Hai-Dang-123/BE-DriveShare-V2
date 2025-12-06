using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize]
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
            var result = await _tripDeliveryIssueService.ReportIssueAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        // API dành cho Khách vãng lai (Link từ Email)
        // POST api/TripDeliveryIssue/contact-report?accessToken=...
        [HttpPost("contact-report")]
        //[AllowAnonymous] // Quan trọng: Không chặn JWT
        public async Task<IActionResult> ReportByContact([FromForm] TripDeliveryIssueCreateDTO dto, [FromQuery] string accessToken)
        {
            var result = await _tripDeliveryIssueService.ReportIssueForContactAsync(dto, accessToken);
            return StatusCode(result.StatusCode, result);
        }


    }
    }
