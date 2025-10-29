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
    public class ContractTermService : IContractTermService
    {
        private readonly IGenericRepository<ContractTerm> _contractTermRepo;
        private readonly IGenericRepository<ContractTemplate> _contractTemplateRepo;
        private readonly IUnitOfWork _unitOfWork;

        public ContractTermService(
            IGenericRepository<ContractTerm> contractTermRepo,
            IGenericRepository<ContractTemplate> contractTemplateRepo,
            IUnitOfWork unitOfWork)
        {
            _contractTermRepo = contractTermRepo;
            _contractTemplateRepo = contractTemplateRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> CreateAsync(ContractTermCreateDTO dto)
        {
            var template = await _contractTemplateRepo.GetByIdAsync(dto.ContractTemplateId);
            if (template == null)
                return new ResponseDTO { IsSuccess = false, Message = "Contract template not found" };

            var term = new ContractTerm
            {
                ContractTermId = Guid.NewGuid(),
                Content = dto.Content,
                Order = dto.Order,
                ContractTemplateId = dto.ContractTemplateId
            };

            await _contractTermRepo.AddAsync(term);
            await _unitOfWork.SaveChangeAsync();

            var result = new ContractTermDetailDTO
            {
                ContractTermId = term.ContractTermId,
                Content = term.Content,
                Order = term.Order,
                ContractTemplateId = term.ContractTemplateId
            };

            return new ResponseDTO { IsSuccess = true, Message = "Contract term created successfully", Result = result };
        }

        public async Task<ResponseDTO> UpdateAsync(ContractTermUpdateDTO dto)
        {
            var term = await _contractTermRepo.GetByIdAsync(dto.ContractTermId);
            if (term == null)
                return new ResponseDTO { IsSuccess = false, Message = "Contract term not found" };

            term.Content = dto.Content ?? term.Content;
            term.Order = dto.Order ?? term.Order;

            await _contractTermRepo.UpdateAsync(term);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseDTO { IsSuccess = true, Message = "Contract term updated successfully" };
        }

        public async Task<ResponseDTO> SoftDeleteAsync(Guid id)
        {
            var term = await _contractTermRepo.GetByIdAsync(id);
            if (term == null)
                return new ResponseDTO { IsSuccess = false, Message = "Contract term not found" };

            await _contractTermRepo.DeleteAsync(id);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseDTO { IsSuccess = true, Message = "Contract term deleted successfully" };
        }

        public async Task<ResponseDTO> GetAllAsync(Guid contractTemplateId)
        {
            var terms = await _contractTermRepo.GetAllAsync(t => t.ContractTemplateId == contractTemplateId);
            var result = terms
                .OrderBy(t => t.Order)
                .Select(t => new ContractTermDTO
                {
                    ContractTermId = t.ContractTermId,
                    Content = t.Content,
                    Order = t.Order,
                    ContractTemplateId = t.ContractTemplateId
                })
                .ToList();

            return new ResponseDTO { IsSuccess = true, Result = result };
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            var term = await _contractTermRepo.GetByIdAsync(id);
            if (term == null)
                return new ResponseDTO { IsSuccess = false, Message = "Contract term not found" };

            var dto = new ContractTermDetailDTO
            {
                ContractTermId = term.ContractTermId,
                Content = term.Content,
                Order = term.Order,
                ContractTemplateId = term.ContractTemplateId
            };

            return new ResponseDTO { IsSuccess = true, Result = dto };
        }
    }
}
