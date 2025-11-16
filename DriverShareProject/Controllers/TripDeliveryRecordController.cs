using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TripDeliveryRecordController : ControllerBase
    {
        private readonly ITripDeliveryRecordService _tripDeliveryRecordService;
        public TripDeliveryRecordController(ITripDeliveryRecordService tripDeliveryRecordService)
        {
            _tripDeliveryRecordService = tripDeliveryRecordService;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateTripDeliveryRecord([FromBody] TripDeliveryRecordCreateDTO dto)
        {
            var result = await _tripDeliveryRecordService.CreateTripDeliveryRecordAsync(dto);
            return StatusCode(result.StatusCode, result);
        }
        [HttpGet("Get-TripDelivery-by-TripId")]
        public async Task<IActionResult> GetTripDeliveryRecordsByTripId([FromQuery] Guid tripId)
        {
            var result = await _tripDeliveryRecordService.GetByTripIdAsync(tripId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("Get-TripDelivery-by-Id")]
        public async Task<IActionResult> GetTripDeliveryRecordById([FromQuery] Guid tripDeliveryRecordId)
        {
            var result = await _tripDeliveryRecordService.GetByIdAsync(tripDeliveryRecordId);
            return StatusCode(result.StatusCode, result);
        }
        [HttpPost("sign-delivery-record")]
        public async Task<IActionResult> SignDeliveryRecord([FromQuery] Guid tripDeliveryRecordId)
        {
            var result = await _tripDeliveryRecordService.SignDeliveryRecordAsync(tripDeliveryRecordId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// [User] Lấy tất cả biên bản liên quan đến user (có phân trang).
        /// (Admin thấy tất cả, Owner/Driver thấy của mình)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllRecords([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var response = await _tripDeliveryRecordService.GetAllAsync(pageNumber, pageSize);
            return StatusCode(response.StatusCode, response);
        }

       

      
    }
}
