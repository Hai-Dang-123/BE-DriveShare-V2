using BLL.Services.Interface;
using Common.DTOs;
using DAL.Entities;
using DAL.UnitOfWork;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class ContractTermService : IContractTermService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ContractTermService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> CreateAsync(ContractTermCreateDTO dto)
        {
            try
            {
                var template = await _unitOfWork.ContractTemplateRepo.GetByIdAsync(dto.ContractTemplateId);
                if (template == null)
                    return new ResponseDTO("Contract template not found", 404, false);

                var term = new ContractTerm
                {
                    ContractTermId = Guid.NewGuid(),
                    Content = dto.Content,
                    Order = dto.Order,
                    ContractTemplateId = dto.ContractTemplateId
                };

                await _unitOfWork.ContractTermRepo.AddAsync(term);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Create ContractTerm Successfully !!!", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Error at saving ContractTerm", 500, false, ex.Message);
            }
        }

        public async Task<ResponseDTO> UpdateAsync(ContractTermUpdateDTO dto)
        {
            var term = await _unitOfWork.ContractTermRepo.GetByIdAsync(dto.ContractTermId);
            if (term == null)
                return new ResponseDTO("Contract term not found", 404, false);

            term.Content = dto.Content ?? term.Content;
            term.Order = dto.Order ?? term.Order;

            await _unitOfWork.ContractTermRepo.UpdateAsync(term);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseDTO("Contract term updated successfully", 200, true);
        }

        public async Task<ResponseDTO> SoftDeleteAsync(Guid id)
        {
            var term = await _unitOfWork.ContractTermRepo.GetByIdAsync(id);
            if (term == null)
                return new ResponseDTO("Contract term not found", 404, false);

            await _unitOfWork.ContractTermRepo.DeleteAsync(id);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseDTO("Contract term deleted successfully", 200, true);
        }

        public async Task<ResponseDTO> GetAllAsync(Guid contractTemplateId)
        {
            var terms = await _unitOfWork.ContractTermRepo.GetAllAsync(t => t.ContractTemplateId == contractTemplateId);
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

            return new ResponseDTO("Get all ContractTerm successfully", 200, true, result);
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            var term = await _unitOfWork.ContractTermRepo.GetByIdAsync(id);
            if (term == null)
                return new ResponseDTO("Contract term not found", 404, false);

            var dto = new ContractTermDetailDTO
            {
                ContractTermId = term.ContractTermId,
                Content = term.Content,
                Order = term.Order,
                ContractTemplateId = term.ContractTemplateId
            };

            return new ResponseDTO("Get ContractTerm successfully", 200, true, dto);
        }
    }
}
