using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace DriverShareProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContractTermController : ControllerBase
    {
        private readonly IContractTermService _contractTermService;

        public ContractTermController(IContractTermService contractTermService)
        {
            _contractTermService = contractTermService;
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromForm] ContractTermCreateDTO dto)
        {
            var result = await _contractTermService.CreateAsync(dto);
            return Ok(result);
        }

        [HttpPut("update")]
        public async Task<IActionResult> Update([FromForm] ContractTermUpdateDTO dto)
        {
            var result = await _contractTermService.UpdateAsync(dto);
            return Ok(result);
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _contractTermService.SoftDeleteAsync(id);
            return Ok(result);
        }

        [HttpGet("getAll/{templateId}")]
        public async Task<IActionResult> GetAll(Guid templateId)
        {
            var result = await _contractTermService.GetAllAsync(templateId);
            return Ok(result);
        }

        [HttpGet("getById/{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _contractTermService.GetByIdAsync(id);
            return Ok(result);
        }
    }
}
