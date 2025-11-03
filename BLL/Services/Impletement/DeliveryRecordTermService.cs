using BLL.Services.Interface;
using Common.DTOs;
using DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class DeliveryRecordTermService : IDeliveryRecordTermService
    {
        private readonly DbContext _context;

        public DeliveryRecordTermService(DbContext context)
        {
            _context = context;
        }

        public async Task<ResponseDTO> CreateAsync(DeliveryRecordTermDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Content))
            {
                return new ResponseDTO("Content cannot be empty", 400, false);
            }

            var entity = new DeliveryRecordTerm
            {
                DeliveryRecordTermId = Guid.NewGuid(),
                DeliveryRecordTemplateId = dto.DeliveryRecordTemplateId,
                Content = dto.Content,
                DisplayOrder = dto.DisplayOrder
            };

            _context.Set<DeliveryRecordTerm>().Add(entity);
            await _context.SaveChangesAsync();

            dto.DeliveryRecordTermId = entity.DeliveryRecordTermId;

            return new ResponseDTO("Created successfully", 201, true, dto);
        }

        public async Task<ResponseDTO> UpdateAsync(Guid id, DeliveryRecordTermDTO dto)
        {
            var entity = await _context.Set<DeliveryRecordTerm>().FindAsync(id);
            if (entity == null)
            {
                return new ResponseDTO("Term not found", 404, false);
            }

            entity.Content = dto.Content;
            entity.DisplayOrder = dto.DisplayOrder;

            await _context.SaveChangesAsync();

            return new ResponseDTO("Updated successfully", 200, true, dto);
        }

        public async Task<ResponseDTO> DeleteAsync(Guid id)
        {
            var entity = await _context.Set<DeliveryRecordTerm>().FindAsync(id);
            if (entity == null)
            {
                return new ResponseDTO("Term not found", 404, false);
            }

            _context.Set<DeliveryRecordTerm>().Remove(entity);
            await _context.SaveChangesAsync();

            return new ResponseDTO("Deleted successfully", 200, true);
        }

        public async Task<ResponseDTO> GetAllAsync()
        {
            var terms = await _context.Set<DeliveryRecordTerm>()
                .Select(term => new DeliveryRecordTermDTO
                {
                    DeliveryRecordTermId = term.DeliveryRecordTermId,
                    DeliveryRecordTemplateId = term.DeliveryRecordTemplateId,
                    Content = term.Content,
                    DisplayOrder = term.DisplayOrder
                })
                .ToListAsync();

            return new ResponseDTO("Retrieved successfully", 200, true, terms);
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            var entity = await _context.Set<DeliveryRecordTerm>().FindAsync(id);
            if (entity == null)
            {
                return new ResponseDTO("Term not found", 404, false);
            }

            var dto = new DeliveryRecordTermDTO
            {
                DeliveryRecordTermId = entity.DeliveryRecordTermId,
                DeliveryRecordTemplateId = entity.DeliveryRecordTemplateId,
                Content = entity.Content,
                DisplayOrder = entity.DisplayOrder
            };

            return new ResponseDTO("Retrieved successfully", 200, true, dto);
        }
    }
}