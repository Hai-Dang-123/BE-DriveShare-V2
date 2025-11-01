using BLL.Services.Interface;
using Common.DTOs;
using Common.Enums.Type;
using DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class DeliveryRecordTemplateService : IDeliveryRecordTemplateService
    {
        private readonly DbContext _context;

        public DeliveryRecordTemplateService(DbContext context)
        {
            _context = context;
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

            _context.Set<DeliveryRecordTemplate>().Add(entity);
            await _context.SaveChangesAsync();


            return new ResponseDTO("Created successfully", 201, true, dto);
        }

        public async Task<ResponseDTO> UpdateAsync(Guid id, DeliveryRecordTemplateDTO dto)
        {
            var entity = await _context.Set<DeliveryRecordTemplate>().FindAsync(id);
            if (entity == null)
            {
                return new ResponseDTO("Template not found", 404, false);
            }

            entity.TemplateName = dto.TemplateName;
            entity.Version = dto.Version;
            entity.Type = Enum.Parse<DeliveryRecordType>(dto.Type);

            await _context.SaveChangesAsync();

            return new ResponseDTO("Updated successfully", 200, true, dto);
        }

        public async Task<ResponseDTO> DeleteAsync(Guid id)
        {
            var entity = await _context.Set<DeliveryRecordTemplate>().FindAsync(id);
            if (entity == null)
            {
                return new ResponseDTO("Template not found", 404, false);
            }

            _context.Set<DeliveryRecordTemplate>().Remove(entity);
            await _context.SaveChangesAsync();

            return new ResponseDTO("Deleted successfully", 200, true);
        }

        public async Task<ResponseDTO> GetAllAsync()
        {
            var templates = await _context.Set<DeliveryRecordTemplate>()
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
            var entity = await _context.Set<DeliveryRecordTemplate>().FindAsync(id);
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
    }
}