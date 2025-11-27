using BLL.Services.Interface;
using Common.DTOs;
using Common.Enums.Type;
using DAL.Entities;
using DAL.Repositories.Interface;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class ContractTemplateService : IContractTemplateService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ContractTemplateService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> CreateAsync(ContractTemplateCreateDTO dto)
        {
            try
            {

                var template = new ContractTemplate
                {
                    ContractTemplateId = Guid.NewGuid(),
                    ContractTemplateName = dto.ContractTemplateName,
                    Version = dto.Version,
                    Type = dto.Type,
                    CreatedAt = DateTime.UtcNow
                };


                await _unitOfWork.ContractTemplateRepo.AddAsync(template);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Create ContractTemplate Successfully !!!", 200, true);
            } catch (Exception ex)
            {
                return new ResponseDTO("Error at saving ContractTemplate" , 500, false);
            }
        }

        public async Task<ResponseDTO> UpdateAsync(ContractTemplateUpdateDTO dto)
        {
            var template = await _unitOfWork.ContractTemplateRepo.GetByIdAsync(dto.ContractTemplateId);
            if (template == null)
                return new ResponseDTO { IsSuccess = false, Message = "Template not found" };

            template.ContractTemplateName = dto.ContractTemplateName ?? template.ContractTemplateName;
            template.Version = dto.Version ?? template.Version;
            if (dto.Type.HasValue)
                template.Type = dto.Type.Value;

            await _unitOfWork.ContractTemplateRepo.UpdateAsync(template);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseDTO { IsSuccess = true, Message = "Contract template updated successfully" };
        }

        public async Task<ResponseDTO> SoftDeleteAsync(Guid id)
        {
            var template = await _unitOfWork.ContractTemplateRepo.GetByIdAsync(id);
            if (template == null)
                return new ResponseDTO { IsSuccess = false, Message = "Template not found" };

            await _unitOfWork.ContractTemplateRepo.DeleteAsync(id);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseDTO { IsSuccess = true, Message = "Template soft-deleted successfully" };
        }

        public async Task<ResponseDTO> GetAllAsync()
        {
            var templates = await _unitOfWork.ContractTemplateRepo.GetAllAsync();
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
            var template = await _unitOfWork.ContractTemplateRepo.GetByIdAsync(id);
            if (template == null)
                return new ResponseDTO { IsSuccess = false, Message = "Template not found" };

            // 🔹 Lấy danh sách ContractTerm liên quan
            var terms = await _unitOfWork.ContractTermRepo.GetAllAsync(t => t.ContractTemplateId == id);
            var termDtos = terms
                .OrderBy(t => t.Order)
                .Select(t => new ContractTermDTO
                {
                    ContractTermId = t.ContractTermId,
                    Content = t.Content,
                    Order = t.Order,
                    ContractTemplateId = t.ContractTemplateId
                })
                .ToList();

            // 🔹 Gộp tất cả lại thành DTO tổng
            var dto = new ContractTemplateDetailDTO
            {
                ContractTemplateId = template.ContractTemplateId,
                ContractTemplateName = template.ContractTemplateName,
                Version = template.Version,
                CreatedAt = template.CreatedAt,
                Type = template.Type,
                ContractTerms = termDtos   // 👈 Thêm phần này
            };

            return new ResponseDTO
            {
                IsSuccess = true,
                Message = "Get ContractTemplate successfully",
                Result = dto
            };
        }

        public async Task<ResponseDTO> GetLatestByTypeAsync(ContractType type)
        {
            try
            {
                // 1. Lấy IQueryable (Giả định .GetAll() trả về IQueryable)
                //    VÀ Include các Terms (theo yêu cầu của bạn)
                var query = _unitOfWork.ContractTemplateRepo.GetAll()
                    .AsNoTracking()
                    .Include(t => t.ContractTerms); // 👈 Include Terms

                // 2. Lọc theo Type và Sắp xếp để tìm cái MỚI NHẤT
                // (Dùng CreateAt là an toàn nhất để xác định "mới nhất")
                var template = await query
                    .Where(t => t.Type == type)
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                // 3. Kiểm tra Not Found
                if (template == null)
                {
                    return new ResponseDTO($"No active template found for type: {type}", 404, false);
                }

                // 4. Map (Dùng logic y hệt GetByIdAsync của bạn)

                // 🔹 Lấy danh sách ContractTerm (đã được Include)
                var termDtos = template.ContractTerms
                    .OrderBy(t => t.Order) // Sắp xếp theo thứ tự
                    .Select(t => new ContractTermDTO
                    {
                        ContractTermId = t.ContractTermId,
                        Content = t.Content,
                        Order = t.Order,
                        ContractTemplateId = t.ContractTemplateId
                    })
                    .ToList();

                // 🔹 Gộp tất cả lại thành DTO tổng
                var dto = new ContractTemplateDetailDTO
                {
                    ContractTemplateId = template.ContractTemplateId,
                    ContractTemplateName = template.ContractTemplateName,
                    Version = template.Version,
                    CreatedAt = template.CreatedAt,
                    Type = template.Type,
                    ContractTerms = termDtos // 👈 Đã thêm Terms
                };

                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = 200, // Thêm StatusCode
                    Message = "Get latest ContractTemplate by type successfully",
                    Result = dto
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting latest template: {ex.Message}", 500, false);
            }
        }

    }
}
