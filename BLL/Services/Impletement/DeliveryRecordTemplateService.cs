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

        public async Task<ResponseDTO> GetAllAsync(int pageNumber, int pageSize)
        {
            try
            {
                var query = _unitOfWork.DeliveryRecordTemplateRepo.GetAll()
                    .AsNoTracking()
                    // Vì bạn có logic Soft Delete, chúng ta LỌC BỎ những cái đã xóa
                    .Where(t => t.Status != DeliveryRecordTemplateStatus.DELETED)
                    .Include(t => t.DeliveryRecordTerms); // Eager loading DeliveryRecordTerms

                // 1. Đếm tổng số (trên DB)
                var totalCount = await query.CountAsync();

                // 2. Lấy dữ liệu của trang (trên DB)
                var templates = await query
                    .OrderByDescending(t => t.CreatedAt) // Thêm OrderBy cho phân trang ổn định
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(t => new DeliveryRecordTemplateDTO
                    {
                        DeliveryRecordTemplateId = t.DeliveryRecordTemplateId,
                        TemplateName = t.TemplateName,
                        Version = t.Version,
                        Type = t.Type.ToString(),
                        CreatedAt = t.CreatedAt,
                        // Sắp xếp các terms con theo DisplayOrder
                        DeliveryRecordTerms = t.DeliveryRecordTerms
                                               .OrderBy(term => term.DisplayOrder)
                                               .Select(term => new DeliveryRecordTermDTO
                                               {
                                                   DeliveryRecordTermId = term.DeliveryRecordTermId,
                                                   DeliveryRecordTemplateId = term.DeliveryRecordTemplateId,
                                                   Content = term.Content,
                                                   DisplayOrder = term.DisplayOrder
                                               }).ToList()
                    })
                    .ToListAsync();

                // 3. Tạo đối tượng PaginatedDTO
                var paginatedResult = new PaginatedDTO<DeliveryRecordTemplateDTO>(templates, totalCount, pageNumber, pageSize);

                return new ResponseDTO("Retrieved successfully", 200, true, paginatedResult);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting templates: {ex.Message}", 500, false);
            }
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

        public async Task<ResponseDTO> GetLatestDeliveryRecordTemplateByTypeAsync(DeliveryRecordType type)
        {
            try
            {
                // 1. Lấy IQueryable và Include Terms
                var query = _unitOfWork.DeliveryRecordTemplateRepo.GetAll()
                    .AsNoTracking()
                    .Include(t => t.DeliveryRecordTerms); // 👈 Include Terms

                // 2. Lọc và Sắp xếp
                var template = await query
                    .Where(t => t.Type == type && t.Status == DeliveryRecordTemplateStatus.ACTIVE) // Chỉ lấy Active
                    .OrderByDescending(t => t.CreatedAt) // Lấy cái mới nhất
                    .FirstOrDefaultAsync();

                // 3. Kiểm tra Not Found
                if (template == null)
                {
                    return new ResponseDTO($"Không tìm thấy DeliveryRecordTemplate (ACTIVE) nào cho loại: {type}", 404, false);
                }

                // 4. Map Terms (giống hàm GetAllAsync)
                var termDtos = template.DeliveryRecordTerms
                    .OrderBy(term => term.DisplayOrder)
                    .Select(term => new DeliveryRecordTermDTO
                    {
                        DeliveryRecordTermId = term.DeliveryRecordTermId,
                        DeliveryRecordTemplateId = term.DeliveryRecordTemplateId,
                        Content = term.Content,
                        DisplayOrder = term.DisplayOrder
                    }).ToList();

                // 5. Map DTO chính
                var dto = new DeliveryRecordTemplateDTO
                {
                    DeliveryRecordTemplateId = template.DeliveryRecordTemplateId,
                    TemplateName = template.TemplateName,
                    Version = template.Version,
                    Type = template.Type.ToString(),
                    CreatedAt = template.CreatedAt,
                    DeliveryRecordTerms = termDtos // 👈 Gán Terms
                };

                return new ResponseDTO("Retrieved latest template by type successfully", 200, true, dto);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting latest template: {ex.Message}", 500, false);
            }
        }
    }
}