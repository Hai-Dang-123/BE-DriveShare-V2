using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TripController : ControllerBase
    {
        private readonly ITripService _tripService;

        public TripController(ITripService tripService)
        {
            _tripService = tripService;
        }

        [HttpPost("create-for-owner")]
        public async Task<IActionResult> CreateForOwner([FromBody] TripCreateDTO dto)
        {
            var result = await _tripService.CreateForOwnerAsync(dto);
            return Ok(result);
        }

        [HttpPut("change-status")]
        public async Task<IActionResult> ChangeStatus([FromBody] ChangeTripStatusDTO dto)
        {
            var result = await _tripService.ChangeTripStatusAsync(dto);
            return Ok(result);
        }
        [HttpGet("owner/{ownerId}")]
        public async Task<IActionResult> GetAllTripByOwnerId(Guid ownerId)
        {
            var result = await _tripService.GetAllTripByOwnerIdAsync(ownerId);
            return Ok(result);
        }
        [HttpGet("driver/{driverId}")]
        public async Task<IActionResult> GetAllTripByDriverId(Guid driverId)
        {
            var response = await _tripService.GetAllTripByDriverIdAsync(driverId);
            return StatusCode(response.StatusCode, response);
        }
        [HttpGet("{tripId}")]
        public async Task<IActionResult> GetTripById(Guid tripId)
        {
            var response = await _tripService.GetTripByIdAsync(tripId);
            return StatusCode(response.StatusCode, response);
        }
    }
}
