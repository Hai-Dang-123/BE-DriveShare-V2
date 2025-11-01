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
    public class TripContactController : ControllerBase
    {
        private readonly ITripContactService _tripContactService; 
        public TripContactController(ITripContactService tripContactService)
        {
            _tripContactService = tripContactService;
        }
        [HttpPost("create-trip-contact")]
        public async Task<IActionResult> CreateTripContact([FromBody] TripContactCreateDTO tripContactDTO)
        {
            var response = await _tripContactService.CreateTripContactAsync(tripContactDTO);
            return StatusCode(response.StatusCode, response);
  
        }
    }
}
