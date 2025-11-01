using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TripProviderContractController : ControllerBase
    {
        private readonly ITripProviderContractService _tripProviderContractService;

        public TripProviderContractController(ITripProviderContractService tripProviderContractService)
        {
            _tripProviderContractService = tripProviderContractService;
        }
        [HttpPost("create")]
        public async Task<IActionResult> CreateContract([FromBody] CreateTripProviderContractDTO dto)
        {
            var result = await _tripProviderContractService.CreateAsync(dto);
            return StatusCode(result.StatusCode, result);
        }
        [HttpPut("sign/{contractId}")]
        public async Task<IActionResult> SignContract(Guid contractId)
        {
            var response = await _tripProviderContractService.SignAsync(contractId);
            return StatusCode(response.StatusCode, response);
        }
        [HttpGet("{contractId}")]
        public async Task<IActionResult> GetContractById(Guid contractId)
        {
            var response = await _tripProviderContractService.GetByIdAsync(contractId);
            return StatusCode(response.StatusCode, response);
        }
    }
}
