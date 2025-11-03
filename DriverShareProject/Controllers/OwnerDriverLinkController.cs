using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OwnerDriverLinkController : ControllerBase
    {
        private readonly IOwnerDriverLinkService _ownerDriverLinkService;
        public OwnerDriverLinkController (IOwnerDriverLinkService ownerDriverLinkService)
        {
            _ownerDriverLinkService = ownerDriverLinkService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOwnerDriverLinkAsync (CreateOwerDriverLinkDTO dto)
        {
            var response = await _ownerDriverLinkService.CreateOwnerDriverLinkAsync (dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("change-status")]
        public async Task<IActionResult> ChangeStatusAsync (ChangeStatusOwnerDriverLinkDTO dto)
        {
            var response = await _ownerDriverLinkService.ChangeStatusAsync (dto);
            return StatusCode(response.StatusCode, response);
        }
    }
}
