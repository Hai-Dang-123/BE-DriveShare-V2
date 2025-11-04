using BLL.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TripRouteController : ControllerBase
    {
        private readonly ITripRouteService _tripRouteService;
        public TripRouteController(ITripRouteService tripRouteService)
        {
            _tripRouteService = tripRouteService;
        }

        [HttpPost("generate/{tripId}")]
        public async Task<IActionResult> GenerateTripRouteAsync(Guid tripId)
        {
            var response = await _tripRouteService.GenerateTripRouteAsync(tripId);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("{tripRouteId}")]
        public async Task<IActionResult> GetTripRouteByIdAsync(Guid tripRouteId)
        {
            var response = await _tripRouteService.GetTripRouteByIdAsync(tripRouteId);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("by-trip/{tripId}")]
        public async Task<IActionResult> GetRouteForTripAsync(Guid tripId)
        {
            var response = await _tripRouteService.GetRouteForTripAsync(tripId);
            return StatusCode(response.StatusCode, response);
        }
    }
}
