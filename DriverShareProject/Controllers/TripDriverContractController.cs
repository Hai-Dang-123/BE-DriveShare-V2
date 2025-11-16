using BLL.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TripDriverContractController : ControllerBase
    {
        private readonly ITripDriverContractService _tripDriverContractService;
        public TripDriverContractController(ITripDriverContractService tripDriverContractService)
        {
            _tripDriverContractService = tripDriverContractService;
        }

        /// <summary>
        /// [Owner/Driver] Lấy danh sách Hợp đồng Tài xế (có phân trang).
        /// </summary>
        [HttpGet("driver-contracts")]
        public async Task<IActionResult> GetAllDriverContracts([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var response = await _tripDriverContractService.GetAllAsync(pageNumber, pageSize);
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// [Owner/Driver] Lấy chi tiết Hợp đồng Tài xế.
        /// </summary>
        [HttpGet("driver-contracts/{id}")]
        public async Task<IActionResult> GetDriverContractById(Guid id)
        {
            var response = await _tripDriverContractService.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }
    }
}
