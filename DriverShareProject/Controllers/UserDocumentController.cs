using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserDocumentController : ControllerBase
    {
        private readonly IUserDocumentService _userDocumentService;

        public UserDocumentController(IUserDocumentService userDocumentService)
        {
            _userDocumentService = userDocumentService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var response = await _userDocumentService.GetAllAsync();
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var response = await _userDocumentService.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UserDocumentDTO dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ResponseDTO("Invalid data", 400, false));
            }

            var response = await _userDocumentService.CreateAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UserDocumentDTO dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ResponseDTO("Invalid data", 400, false));
            }

            var response = await _userDocumentService.UpdateAsync(id, dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var response = await _userDocumentService.DeleteAsync(id);
            return StatusCode(response.StatusCode, response);
        }
    }
}