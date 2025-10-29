using BLL.Services.Interface;
using Common.DTOs;
using DAL.Entities;
using DAL.Repositories.Interface;
using DAL.UnitOfWork;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class ContractTemplateService : IContractTemplateService
    {
        private readonly IGenericRepository<ContractTemplate> _contractTemplateRepo;
        private readonly IUnitOfWork _unitOfWork;

        public ContractTemplateService(IGenericRepository<ContractTemplate> contractTemplateRepo, IUnitOfWork unitOfWork)
        {
            _contractTemplateRepo = contractTemplateRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> CreateAsync(ContractTemplateCreateDTO dto)
        {
            var template = new ContractTemplate
            {
                ContractTemplateId = Guid.NewGuid(),
                ContractTemplateName = dto.ContractTemplateName,
                Version = dto.Version,
                Type = dto.Type,
                CreatedAt = DateTime.UtcNow
            };

            await _contractTemplateRepo.AddAsync(template);
            await _unitOfWork.SaveChangeAsync();

            var result = new ContractTemplateDetailDTO
            {
                ContractTemplateId = template.ContractTemplateId,
                ContractTemplateName = template.ContractTemplateName,
                Version = template.Version,
                Type = template.Type,
                CreatedAt = template.CreatedAt
            };

            return new ResponseDTO { IsSuccess = true, Message = "Contract template created successfully", Result = result };
        }

        public async Task<ResponseDTO> UpdateAsync(ContractTemplateUpdateDTO dto)
        {
            var template = await _contractTemplateRepo.GetByIdAsync(dto.ContractTemplateId);
            if (template == null)
                return new ResponseDTO { IsSuccess = false, Message = "Template not found" };

            template.ContractTemplateName = dto.ContractTemplateName ?? template.ContractTemplateName;
            template.Version = dto.Version ?? template.Version;
            if (dto.Type.HasValue)
                template.Type = dto.Type.Value;

            await _contractTemplateRepo.UpdateAsync(template);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseDTO { IsSuccess = true, Message = "Contract template updated successfully" };
        }

        public async Task<ResponseDTO> SoftDeleteAsync(Guid id)
        {
            var template = await _contractTemplateRepo.GetByIdAsync(id);
            if (template == null)
                return new ResponseDTO { IsSuccess = false, Message = "Template not found" };

            await _contractTemplateRepo.DeleteAsync(id);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseDTO { IsSuccess = true, Message = "Template soft-deleted successfully" };
        }

        public async Task<ResponseDTO> GetAllAsync()
        {
            var templates = await _contractTemplateRepo.GetAllAsync();
            var result = templates.Select(t => new ContractTemplateDTO
            {
                ContractTemplateId = t.ContractTemplateId,
                ContractTemplateName = t.ContractTemplateName,
                Version = t.Version,
                CreatedAt = t.CreatedAt,
                Type = t.Type
            }).ToList();

            return new ResponseDTO { IsSuccess = true, Result = result };
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            var template = await _contractTemplateRepo.GetByIdAsync(id);
            if (template == null)
                return new ResponseDTO { IsSuccess = false, Message = "Template not found" };

            var dto = new ContractTemplateDetailDTO
            {
                ContractTemplateId = template.ContractTemplateId,
                ContractTemplateName = template.ContractTemplateName,
                Version = template.Version,
                CreatedAt = template.CreatedAt,
                Type = template.Type
            };

            return new ResponseDTO { IsSuccess = true, Result = dto };
        }
    }
}
