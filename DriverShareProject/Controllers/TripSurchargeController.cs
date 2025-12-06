using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TripSurchargeController : ControllerBase
    {
        private readonly ITripSurchargeService _surchargeService;

        public TripSurchargeController(ITripSurchargeService surchargeService)
        {
            _surchargeService = surchargeService;
        }

        // POST api/TripSurcharge/create (Owner)
        [HttpPost("create")]
        //[Authorize(Roles = "Owner")]
        public async Task<IActionResult> Create([FromBody] TripSurchargeCreateDTO dto)
        {
            var result = await _surchargeService.CreateSurchargeAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        // POST api/TripSurcharge/contact-create (Contact Claim)
        [HttpPost("contact-create")]
        //[AllowAnonymous]
        public async Task<IActionResult> CreateByContact([FromBody] TripSurchargeCreateDTO dto, [FromQuery] string accessToken)
        {

            var result = await _surchargeService.CreateSurchargeForContactAsync(dto, accessToken);
            return StatusCode(result.StatusCode, result);
        }

        // GET api/TripSurcharge/get-by-trip/{tripId}
        [HttpGet("get-by-trip/{tripId}")]
        //[Authorize]
        public async Task<IActionResult> GetByTrip([FromRoute] Guid tripId)
        {
            var result = await _surchargeService.GetSurchargesByTripIdAsync(tripId);
            return StatusCode(result.StatusCode, result);
        }

        // PUT api/TripSurcharge/update-status
        [HttpPut("update-status")]
        //[Authorize(Roles = "Owner,Admin")]
        public async Task<IActionResult> UpdateStatus([FromBody] UpdateSurchargeStatusDTO dto)
        {
            var result = await _surchargeService.UpdateStatusAsync(dto);
            return StatusCode(result.StatusCode, result);
        }
    }
}
