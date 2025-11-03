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
    public class UserDocumentService : IUserDocumentService
    {
        private readonly DbContext _context;

        public UserDocumentService(DbContext context)
        {
            _context = context;
        }

        public async Task<ResponseDTO> CreateAsync(UserDocumentDTO dto)
        {
            // Validate dữ liệu
            if (string.IsNullOrWhiteSpace(dto.FrontImageUrl))
            {
                return new ResponseDTO("FrontImageUrl cannot be empty", 400, false);
            }

            if (!Enum.TryParse<DocumentType>(dto.DocumentType, out var documentType))
            {
                return new ResponseDTO("Invalid DocumentType", 400, false);
            }

            if (!Enum.TryParse<VerifileStatus>(dto.Status, out var status))
            {
                return new ResponseDTO("Invalid Status", 400, false);
            }

            var entity = new UserDocument
            {
                UserDocumentId = Guid.NewGuid(),
                UserId = dto.UserId,
                DocumentType = documentType,
                FrontImageUrl = dto.FrontImageUrl,
                FrontImageHash = dto.FrontImageHash,
                BackImageUrl = dto.BackImageUrl,
                BackImageHash = dto.BackImageHash,
                Status = status,
                RejectionReason = dto.RejectionReason,
                CreatedAt = DateTime.UtcNow
            };

            _context.Set<UserDocument>().Add(entity);
            await _context.SaveChangesAsync();

            dto.UserDocumentId = entity.UserDocumentId;
            dto.CreatedAt = entity.CreatedAt;

            return new ResponseDTO("Created successfully", 201, true, dto);
        }

        public async Task<ResponseDTO> UpdateAsync(Guid id, UserDocumentDTO dto)
        {
            var entity = await _context.Set<UserDocument>().FindAsync(id);
            if (entity == null)
            {
                return new ResponseDTO("Document not found", 404, false);
            }

            entity.FrontImageUrl = dto.FrontImageUrl;
            entity.FrontImageHash = dto.FrontImageHash;
            entity.BackImageUrl = dto.BackImageUrl;
            entity.BackImageHash = dto.BackImageHash;
            entity.RejectionReason = dto.RejectionReason;

            await _context.SaveChangesAsync();

            return new ResponseDTO("Updated successfully", 200, true, dto);
        }

        public async Task<ResponseDTO> DeleteAsync(Guid id)
        {
            var entity = await _context.Set<UserDocument>().FindAsync(id);
            if (entity == null)
            {
                return new ResponseDTO("Document not found", 404, false);
            }

            _context.Set<UserDocument>().Remove(entity);
            await _context.SaveChangesAsync();

            return new ResponseDTO("Deleted successfully", 200, true);
        }

        public async Task<ResponseDTO> GetAllAsync()
        {
            var documents = await _context.Set<UserDocument>()
                .Select(doc => new UserDocumentDTO
                {
                    UserDocumentId = doc.UserDocumentId,
                    UserId = doc.UserId,
                    DocumentType = doc.DocumentType.ToString(),
                    FrontImageUrl = doc.FrontImageUrl,
                    FrontImageHash = doc.FrontImageHash,
                    BackImageUrl = doc.BackImageUrl,
                    BackImageHash = doc.BackImageHash,
                    Status = doc.Status.ToString(),
                    RejectionReason = doc.RejectionReason,
                    CreatedAt = doc.CreatedAt,
                    VerifiedAt = doc.VerifiedAt
                })
                .ToListAsync();

            return new ResponseDTO("Retrieved successfully", 200, true, documents);
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            var entity = await _context.Set<UserDocument>().FindAsync(id);
            if (entity == null)
            {
                return new ResponseDTO("Document not found", 404, false);
            }

            var dto = new UserDocumentDTO
            {
                UserDocumentId = entity.UserDocumentId,
                UserId = entity.UserId,
                DocumentType = entity.DocumentType.ToString(),
                FrontImageUrl = entity.FrontImageUrl,
                FrontImageHash = entity.FrontImageHash,
                BackImageUrl = entity.BackImageUrl,
                BackImageHash = entity.BackImageHash,
                Status = entity.Status.ToString(),
                RejectionReason = entity.RejectionReason,
                CreatedAt = entity.CreatedAt,
                VerifiedAt = entity.VerifiedAt
            };

            return new ResponseDTO("Retrieved successfully", 200, true, dto);
        }
    }
}