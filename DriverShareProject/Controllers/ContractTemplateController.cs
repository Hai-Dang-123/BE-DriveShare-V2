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
    public class ContractTemplateController : ControllerBase
    {
        private readonly IContractTemplateService _contractTemplateService;

        public ContractTemplateController(IContractTemplateService contractTemplateService)
        {
            _contractTemplateService = contractTemplateService;
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromForm] ContractTemplateCreateDTO dto)
        {
            var result = await _contractTemplateService.CreateAsync(dto);
            return Ok(result);
        }

        [HttpPut("update")]
        public async Task<IActionResult> Update([FromForm] ContractTemplateUpdateDTO dto)
        {
            var result = await _contractTemplateService.UpdateAsync(dto);
            return Ok(result);
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _contractTemplateService.SoftDeleteAsync(id);
            return Ok(result);
        }

        [HttpGet("getAll")]
        public async Task<IActionResult> GetAll()
        {
            var result = await _contractTemplateService.GetAllAsync();
            return Ok(result);
        }

        [HttpGet("getById/{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _contractTemplateService.GetByIdAsync(id);
            return Ok(result);
        }
    }
}
