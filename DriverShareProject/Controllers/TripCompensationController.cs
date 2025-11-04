using BLL.Services.Impletement;
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
    public class TripCompensationController : ControllerBase
    {
        private readonly ITripCompensationService _tripCompensationService;

        public TripCompensationController(ITripCompensationService tripCompensationService)
        {
            _tripCompensationService = _tripCompensationService;
        }

        [HttpPost("Create-TripCompenstion")]
        public async Task<IActionResult> CreateTripCompensation([FromBody] TripCompensationCreateDTO tripCompensationCreateDTO)
        {
            var response = await _tripCompensationService.CreateTripCompensation(tripCompensationCreateDTO);
            return StatusCode(response.StatusCode, response);
        }

    }
}
