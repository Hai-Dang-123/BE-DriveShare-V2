using BLL.Services.Interface;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class DeliveryRecordTemplateService : IDeliveryRecordTemplateService
    {
        private readonly IUnitOfWork _unitOfWork;

        public DeliveryRecordTemplateService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> CreateAsync(DeliveryRecordTemplateDTO dto)
        {
            var entity = new DeliveryRecordTemplate
            {
                DeliveryRecordTemplateId = Guid.NewGuid(),
                TemplateName = dto.TemplateName,
                Version = dto.Version,
                Type = Enum.Parse<DeliveryRecordType>(dto.Type),
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.DeliveryRecordTemplateRepo.AddAsync(entity);
            await _unitOfWork.SaveChangeAsync();


            dto.DeliveryRecordTemplateId = entity.DeliveryRecordTemplateId;
            dto.CreatedAt = entity.CreatedAt;


            return new ResponseDTO("Created successfully", 201, true, dto);
        }

        public async Task<ResponseDTO> UpdateAsync(Guid id, DeliveryRecordTemplateDTO dto)
        {
            var entity = await _unitOfWork.DeliveryRecordTemplateRepo.GetByIdAsync(id);
            if (entity == null)
            {
                return new ResponseDTO("Template not found", 404, false);
            }

            entity.TemplateName = dto.TemplateName;
            entity.Version = dto.Version;
            entity.Type = Enum.Parse<DeliveryRecordType>(dto.Type);

            await _unitOfWork.SaveChangeAsync();

            return new ResponseDTO("Updated successfully", 200, true, dto);
        }

        public async Task<ResponseDTO> DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.DeliveryRecordTemplateRepo.GetByIdAsync(id);
            if (entity == null)
            {
                return new ResponseDTO("Template not found", 404, false);
            }

            entity.Status = DeliveryRecordTemplateStatus.DELETED;
            await _unitOfWork.DeliveryRecordTemplateRepo.UpdateAsync(entity);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseDTO("Deleted successfully", 200, true);
        }

        public async Task<ResponseDTO> GetAllAsync()
        {
            var templates = await _unitOfWork.DeliveryRecordTemplateRepo.GetAll()
                .Include(t => t.DeliveryRecordTerms) // Eager loading DeliveryRecordTerms
                .Select(t => new DeliveryRecordTemplateDTO
                {
                    DeliveryRecordTemplateId = t.DeliveryRecordTemplateId,
                    TemplateName = t.TemplateName,
                    Version = t.Version,
                    Type = t.Type.ToString(),
                    CreatedAt = t.CreatedAt,
                    DeliveryRecordTerms = t.DeliveryRecordTerms.Select(term => new DeliveryRecordTermDTO
                    {
                        DeliveryRecordTermId = term.DeliveryRecordTermId,
                        Content = term.Content,
                        DisplayOrder = term.DisplayOrder
                    }).ToList()
                })
                .ToListAsync();

            return new ResponseDTO("Retrieved successfully", 200, true, templates);
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.DeliveryRecordTemplateRepo.GetByIdAsync(id);
            if (entity == null)
            {
                return new ResponseDTO("Template not found", 404, false);
            }

            var dto = new DeliveryRecordTemplateDTO
            {
                DeliveryRecordTemplateId = entity.DeliveryRecordTemplateId,
                TemplateName = entity.TemplateName,
                Version = entity.Version,
                Type = entity.Type.ToString(),
                CreatedAt = entity.CreatedAt
            };

            return new ResponseDTO("Retrieved successfully", 200, true, dto);
        }

        public async Task<DeliveryRecordTemplate> GetLatestTemplateByTypeAsync(DeliveryRecordType type)
        {
            // (Giả sử bạn có DeliveryRecordTemplateRepo trong UnitOfWork)
            var query = _unitOfWork.DeliveryRecordTemplateRepo.GetAll()
                .Where(t => t.Type == type && t.Status == DeliveryRecordTemplateStatus.ACTIVE) // Chỉ lấy template đang Active
                .OrderByDescending(t => t.CreatedAt); // Lấy cái mới nhất

            var template = await query.FirstOrDefaultAsync();

            if (template == null)
            {
                // Đây là lỗi hệ thống nghiêm trọng
                throw new Exception($"Không tìm thấy DeliveryRecordTemplate (ACTIVE) nào cho loại: {type}.");
            }

            return template;
        }
    }
}