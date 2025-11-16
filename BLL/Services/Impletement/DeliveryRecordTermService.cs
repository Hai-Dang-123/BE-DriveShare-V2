using BLL.Services.Interface;
using Common.DTOs;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class DeliveryRecordTermService : IDeliveryRecordTermService
    {
        private readonly IUnitOfWork _unitOfWork;

        public DeliveryRecordTermService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // 1. CREATE
        public async Task<ResponseDTO> CreateAsync(DeliveryRecordTermDTO dto)
        {
            try
            {
                // (Kiểm tra xem TemplateId có tồn tại không)
                var templateExists = await _unitOfWork.DeliveryRecordTemplateRepo.GetByIdAsync(dto.DeliveryRecordTemplateId);
                if (templateExists == null)
                {
                    return new ResponseDTO("Associated Template not found", 404, false);
                }

                var entity = new DeliveryRecordTerm
                {
                    DeliveryRecordTermId = Guid.NewGuid(),
                    DeliveryRecordTemplateId = dto.DeliveryRecordTemplateId,
                    Content = dto.Content,
                    DisplayOrder = dto.DisplayOrder
                };

                await _unitOfWork.DeliveryRecordTermRepo.AddAsync(entity);
                await _unitOfWork.SaveChangeAsync();

                dto.DeliveryRecordTermId = entity.DeliveryRecordTermId;
                return new ResponseDTO("Term created successfully", 201, true, dto);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error creating term: {ex.Message}", 500, false);
            }
        }

        // 2. UPDATE
        public async Task<ResponseDTO> UpdateAsync(Guid id, DeliveryRecordTermDTO dto)
        {
            try
            {
                var entity = await _unitOfWork.DeliveryRecordTermRepo.GetByIdAsync(id);
                if (entity == null)
                {
                    return new ResponseDTO("Term not found", 404, false);
                }

                // Cập nhật các trường
                entity.Content = dto.Content;
                entity.DisplayOrder = dto.DisplayOrder;
                // (Không nên cho phép đổi TemplateId)

                await _unitOfWork.DeliveryRecordTermRepo.UpdateAsync(entity);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Term updated successfully", 200, true, dto);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error updating term: {ex.Message}", 500, false);
            }
        }

        // 3. DELETE
        public async Task<ResponseDTO> DeleteAsync(Guid id)
        {
            try
            {
                var entity = await _unitOfWork.DeliveryRecordTermRepo.GetByIdAsync(id);
                if (entity == null)
                {
                    return new ResponseDTO("Term not found", 404, false);
                }

                // Entity 'DeliveryRecordTerm' không có 'Status', nên ta dùng XÓA CỨNG (Hard Delete)
                await _unitOfWork.DeliveryRecordTermRepo.DeleteAsync(id);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Term deleted successfully", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error deleting term: {ex.Message}", 500, false);
            }
        }

        // 4. GET BY ID
        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            try
            {
                var entity = await _unitOfWork.DeliveryRecordTermRepo.GetByIdAsync(id);
                if (entity == null)
                {
                    return new ResponseDTO("Term not found", 404, false);
                }

                // Map sang DTO
                var dto = new DeliveryRecordTermDTO
                {
                    DeliveryRecordTermId = entity.DeliveryRecordTermId,
                    DeliveryRecordTemplateId = entity.DeliveryRecordTemplateId,
                    Content = entity.Content,
                    DisplayOrder = entity.DisplayOrder
                };
                return new ResponseDTO("Retrieved successfully", 200, true, dto);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting term: {ex.Message}", 500, false);
            }
        }

        // 5. GET ALL (PAGINATED)
        public async Task<ResponseDTO> GetAllAsync(int pageNumber, int pageSize)
        {
            try
            {
                var query = _unitOfWork.DeliveryRecordTermRepo.GetAll()
                                .AsNoTracking();

                // 1. Đếm tổng số
                var totalCount = await query.CountAsync();

                // 2. Lấy dữ liệu của trang
                var terms = await query
                    .OrderBy(t => t.DisplayOrder) // Sắp xếp theo thứ tự
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(t => new DeliveryRecordTermDTO
                    {
                        DeliveryRecordTermId = t.DeliveryRecordTermId,
                        DeliveryRecordTemplateId = t.DeliveryRecordTemplateId,
                        Content = t.Content,
                        DisplayOrder = t.DisplayOrder
                    })
                    .ToListAsync();

                // 3. Tạo kết quả phân trang
                var paginatedResult = new PaginatedDTO<DeliveryRecordTermDTO>(terms, totalCount, pageNumber, pageSize);
                return new ResponseDTO("Retrieved all terms successfully", 200, true, paginatedResult);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting terms: {ex.Message}", 500, false);
            }
        }

        // (Optional but Recommended)
        public async Task<ResponseDTO> GetAllByTemplateIdAsync(Guid templateId, int pageNumber, int pageSize)
        {
            try
            {
                var query = _unitOfWork.DeliveryRecordTermRepo.GetAll()
                                .AsNoTracking()
                                .Where(t => t.DeliveryRecordTemplateId == templateId);

                var totalCount = await query.CountAsync();

                var terms = await query
                    .OrderBy(t => t.DisplayOrder)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(t => new DeliveryRecordTermDTO
                    {
                        DeliveryRecordTermId = t.DeliveryRecordTermId,
                        DeliveryRecordTemplateId = t.DeliveryRecordTemplateId,
                        Content = t.Content,
                        DisplayOrder = t.DisplayOrder
                    })
                    .ToListAsync();

                var paginatedResult = new PaginatedDTO<DeliveryRecordTermDTO>(terms, totalCount, pageNumber, pageSize);
                return new ResponseDTO($"Retrieved terms for template {templateId} successfully", 200, true, paginatedResult);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting terms: {ex.Message}", 500, false);
            }
        }
    }
}
