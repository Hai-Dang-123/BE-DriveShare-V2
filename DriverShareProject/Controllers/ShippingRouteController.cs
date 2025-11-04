using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ShippingRouteController : ControllerBase
    {
        private readonly IShippingRouteService _shippingRouteService;
        public ShippingRouteController(IShippingRouteService shippingRouteService)
        {
            _shippingRouteService = shippingRouteService;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateShippingRoute([FromBody] CreateShippingRouteDTO dto)
        {
            var response = await _shippingRouteService.CreateShippingRouteAsync(dto);
            return StatusCode(response.StatusCode, response);
        }
    }
}
