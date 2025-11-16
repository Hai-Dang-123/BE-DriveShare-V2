using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostTripController : ControllerBase
    {
        private readonly IPostTripService _postTripService;

        public PostTripController(IPostTripService postTripService)
        {
            _postTripService = postTripService;
        }

        /// <summary>
        /// [Owner] Tạo một bài đăng tuyển tài xế mới cho chuyến đi.
        /// </summary>
        [HttpPost]
        //[Authorize(Roles = "Owner")] // Chỉ Owner mới được tạo
        public async Task<IActionResult> CreatePostTrip([FromBody] PostTripCreateDTO dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ResponseDTO("Invalid input data", 400, false, ModelState));
            }

            var response = await _postTripService.CreatePostTripAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// [Public] Lấy tất cả bài đăng đang MỞ (OPEN) (có phân trang).
        /// </summary>
        [HttpGet("open")]
        //[AllowAnonymous] // Ai cũng có thể xem
        public async Task<IActionResult> GetAllOpenPostTrips([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var response = await _postTripService.GetAllOpenPostTripsAsync(pageNumber, pageSize);
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// [Owner] Lấy tất cả bài đăng của CHÍNH TÔI (Owner đã đăng nhập) (có phân trang).
        /// </summary>
        [HttpGet("my-posts")]
        //[Authorize(Roles = "Owner")] // Chỉ Owner xem được
        public async Task<IActionResult> GetMyPostTrips([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var response = await _postTripService.GetMyPostTripsAsync(pageNumber, pageSize);
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// [Public] Lấy chi tiết một bài đăng bằng ID.
        /// (Service đã xử lý logic: nếu OPEN thì public, nếu DONE/DELETED thì chỉ Owner)
        /// </summary>
        [HttpGet("{id}")]
        //[AllowAnonymous] // Endpoint public, service sẽ xử lý logic quyền
        public async Task<IActionResult> GetPostTripById(Guid id)
        {
            var response = await _postTripService.GetPostTripByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

    }


}
