using BLL.Services.Interface;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
using DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class VehicleDocumentService : IVehicleDocumentService
    {
        private readonly DbContext _context;

        public VehicleDocumentService(DbContext context)
        {
            _context = context;
        }

        public async Task<ResponseDTO> CreateAsync(VehicleDocumentDTO dto)
        {
            // Validate dữ liệu
            if (string.IsNullOrWhiteSpace(dto.FrontDocumentUrl))
            {
                return new ResponseDTO("FrontDocumentUrl cannot be empty", 400, false);
            }

            if (!Enum.TryParse<DocumentType>(dto.DocumentType, out var documentType))
            {
                return new ResponseDTO("Invalid DocumentType", 400, false);
            }

            if (!Enum.TryParse<VerifileStatus>(dto.Status, out var status))
            {
                return new ResponseDTO("Invalid Status", 400, false);
            }

            var entity = new VehicleDocument
            {
                VehicleDocumentId = Guid.NewGuid(),
                VehicleId = dto.VehicleId,
                DocumentType = documentType,
                FrontDocumentUrl = dto.FrontDocumentUrl,
                BackDocumentUrl = dto.BackDocumentUrl,
                FrontFileHash = dto.FrontFileHash,
                BackFileHash = dto.BackFileHash,
                ExpirationDate = dto.ExpirationDate,
                Status = status,
                AdminNotes = dto.AdminNotes,
                CreatedAt = DateTime.UtcNow,
                RawResultJson = dto.RawResultJson
            };

            _context.Set<VehicleDocument>().Add(entity);
            await _context.SaveChangesAsync();

            dto.VehicleDocumentId = entity.VehicleDocumentId;
            dto.CreatedAt = entity.CreatedAt;

            return new ResponseDTO("Created successfully", 201, true, dto);
        }

        public async Task<ResponseDTO> UpdateAsync(Guid id, VehicleDocumentDTO dto)
        {
            var entity = await _context.Set<VehicleDocument>().FindAsync(id);
            if (entity == null)
            {
                return new ResponseDTO("Document not found", 404, false);
            }

            entity.FrontDocumentUrl = dto.FrontDocumentUrl;
            entity.BackDocumentUrl = dto.BackDocumentUrl;
            entity.FrontFileHash = dto.FrontFileHash;
            entity.BackFileHash = dto.BackFileHash;
            entity.ExpirationDate = dto.ExpirationDate;
            entity.AdminNotes = dto.AdminNotes;

            await _context.SaveChangesAsync();

            return new ResponseDTO("Updated successfully", 200, true, dto);
        }

        public async Task<ResponseDTO> DeleteAsync(Guid id)
        {
            var entity = await _context.Set<VehicleDocument>().FindAsync(id);
            if (entity == null)
            {
                return new ResponseDTO("Document not found", 404, false);
            }

            _context.Set<VehicleDocument>().Remove(entity);
            await _context.SaveChangesAsync();

            return new ResponseDTO("Deleted successfully", 200, true);
        }

        public async Task<ResponseDTO> GetAllAsync()
        {
            var documents = await _context.Set<VehicleDocument>()
                .Select(doc => new VehicleDocumentDTO
                {
                    VehicleDocumentId = doc.VehicleDocumentId,
                    VehicleId = doc.VehicleId,
                    DocumentType = doc.DocumentType.ToString(),
                    FrontDocumentUrl = doc.FrontDocumentUrl,
                    BackDocumentUrl = doc.BackDocumentUrl,
                    FrontFileHash = doc.FrontFileHash,
                    BackFileHash = doc.BackFileHash,
                    ExpirationDate = doc.ExpirationDate,
                    Status = doc.Status.ToString(),
                    AdminNotes = doc.AdminNotes,
                    CreatedAt = doc.CreatedAt,
                    ProcessedAt = doc.ProcessedAt,
                    RawResultJson = doc.RawResultJson
                })
                .ToListAsync();

            return new ResponseDTO("Retrieved successfully", 200, true, documents);
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            var entity = await _context.Set<VehicleDocument>().FindAsync(id);
            if (entity == null)
            {
                return new ResponseDTO("Document not found", 404, false);
            }

            var dto = new VehicleDocumentDTO
            {
                VehicleDocumentId = entity.VehicleDocumentId,
                VehicleId = entity.VehicleId,
                DocumentType = entity.DocumentType.ToString(),
                FrontDocumentUrl = entity.FrontDocumentUrl,
                BackDocumentUrl = entity.BackDocumentUrl,
                FrontFileHash = entity.FrontFileHash,
                BackFileHash = entity.BackFileHash,
                ExpirationDate = entity.ExpirationDate,
                Status = entity.Status.ToString(),
                AdminNotes = entity.AdminNotes,
                CreatedAt = entity.CreatedAt,
                ProcessedAt = entity.ProcessedAt,
                RawResultJson = entity.RawResultJson
            };

            return new ResponseDTO("Retrieved successfully", 200, true, dto);
        }
    }
}