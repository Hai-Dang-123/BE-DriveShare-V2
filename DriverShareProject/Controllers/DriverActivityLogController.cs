using BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DriverActivityLogController : Controller
    {
        private readonly IDriverActivityLogService _driverService;
        public DriverActivityLogController(IDriverActivityLogService driverActivityLogService)
        {
            _driverService = driverActivityLogService;
        }

        // 1. GET MY LOGS
        // GET: api/DriverLog/my-logs?page=1&pageSize=10&logLevel=Warning
        [HttpGet("my-logs")]
        //[Authorize(Roles = "Driver")]
        public async Task<IActionResult> GetMyLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? logLevel = null)
        {
            var result = await _driverService.GetMyLogsAsync(page, pageSize, logLevel);
            return StatusCode(result.StatusCode, result);
        }

        // 2. GET LOGS BY DRIVER ID (Admin/Owner/Staff)
        // GET: api/DriverLog/driver/{driverId}?page=1
        [HttpGet("driver/{driverId}")]
        //[Authorize(Roles = "Admin,Owner,Staff")]
        public async Task<IActionResult> GetLogsByDriverId(Guid driverId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? logLevel = null)
        {
            var result = await _driverService.GetLogsByDriverIdAsync(driverId, page, pageSize, logLevel);
            return StatusCode(result.StatusCode, result);
        }

        // 3. GET ALL LOGS (Admin Only)
        // GET: api/DriverLog/all?search=TaiXeA&logLevel=Critical
        [HttpGet("all")]
        //[Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? logLevel = null)
        {
            var result = await _driverService.GetAllLogsAsync(page, pageSize, search, logLevel);
            return StatusCode(result.StatusCode, result);
        }

        // 4. GET STATS (Admin or Self)
        // GET: api/DriverLog/stats (All) OR api/DriverLog/stats?driverId=...
        [HttpGet("stats")]
        //[Authorize]
        public async Task<IActionResult> GetStats([FromQuery] Guid? driverId = null)
        {
            var result = await _driverService.GetLogStatisticsAsync(driverId);
            return StatusCode(result.StatusCode, result);
        }
    }
}
