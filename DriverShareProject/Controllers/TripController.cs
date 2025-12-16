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

        // ============================================================
        // CREATE TRIP FROM POST (Owner)
        // ============================================================
        [HttpPost("owner-create-from-post")]
        public async Task<IActionResult> CreateTripFromPost([FromBody] TripCreateFromPostDTO dto)
        {
            var result = await _tripService.CreateTripFromPostAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        // ============================================================
        // CHANGE STATUS
        // ============================================================
        [HttpPut("change-status")]
        public async Task<IActionResult> ChangeStatus([FromBody] ChangeTripStatusDTO dto)
        {
            var result = await _tripService.ChangeTripStatusAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        // ADMIN GET ALL
        [HttpGet("all")]
        public async Task<IActionResult> GetAllTrips(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? sortField = null,
            [FromQuery] string? sortDirection = "DESC"
        )
        {
            var response = await _tripService.GetAllAsync(pageNumber, pageSize, search, sortField, sortDirection);
            return StatusCode(response.StatusCode, response);
        }

        // OWNER GET MY TRIPS
        [HttpGet("owner")]
        public async Task<IActionResult> GetAllTripsByOwner(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? sortField = null,
            [FromQuery] string? sortDirection = "DESC"
        )
        {
            var response = await _tripService.GetAllTripsByOwnerAsync(pageNumber, pageSize, search, sortField, sortDirection);
            return StatusCode(response.StatusCode, response);
        }

        // DRIVER GET MY TRIPS
        [HttpGet("driver")]
        public async Task<IActionResult> GetAllTripsByDriver(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? sortField = null,
            [FromQuery] string? sortDirection = "DESC"
        )
        {
            var response = await _tripService.GetAllTripsByDriverAsync(pageNumber, pageSize, search, sortField, sortDirection);
            return StatusCode(response.StatusCode, response);
        }

        // PROVIDER GET MY TRIPS
        [HttpGet("provider")]
        public async Task<IActionResult> GetAllTripsByProvider(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? sortField = null,
            [FromQuery] string? sortDirection = "DESC"
        )
        {
            var response = await _tripService.GetAllTripsByProviderAsync(pageNumber, pageSize, search, sortField, sortDirection);
            return StatusCode(response.StatusCode, response);
        }

        // ============================================================
        // GET TRIP BY ID
        // ============================================================
        [HttpGet("{tripId}")]
        public async Task<IActionResult> GetTripById(Guid tripId)
        {
            var response = await _tripService.GetTripByIdAsync(tripId);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("analyze-drivers/{tripId}")]
        public async Task<IActionResult> AnalyzeTripDrivers([FromRoute] Guid tripId)
        {
            var result = await _tripService.GetTripDriverAnalysisAsync(tripId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("cancel-by-owner")]
        public async Task<IActionResult> CancelTripByOwner([FromBody] CancelTripDTO dto)
        {
           

            var result = await _tripService.CancelTripByOwnerAsync(dto);

            return StatusCode(result.StatusCode, result);
        }


    }
}
