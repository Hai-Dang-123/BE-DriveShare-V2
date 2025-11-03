using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.ValueObjects;
using DAL.Entities;
using DAL.UnitOfWork;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class VehicleService : IVehicleService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;

        public VehicleService(IUnitOfWork unitOfWork, UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
        }

        // CREATE
        public async Task<ResponseDTO> CreateAsync(VehicleCreateDTO dto)
        {
            try
            {
                var ownerId = _userUtility.GetUserIdFromToken();
                if (ownerId == Guid.Empty)
                    return new ResponseDTO("Unauthorized or invalid token", 401, false);

                var vehicle = new Vehicle
                {
                    VehicleId = Guid.NewGuid(),
                    OwnerId = ownerId,
                    VehicleTypeId = dto.VehicleTypeId,
                    PlateNumber = dto.PlateNumber,
                    Model = dto.Model,
                    Brand = dto.Brand,
                    YearOfManufacture = dto.YearOfManufacture,
                    Color = dto.Color,
                    PayloadInKg = dto.PayloadInKg,
                    VolumeInM3 = dto.VolumeInM3,
                    Features = dto.Features ?? new(),
                    CurrentAddress = dto.CurrentAddress,
                    Status = VehicleStatus.ACTIVE,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.VehicleRepo.AddAsync(vehicle);
                await _unitOfWork.SaveChangeAsync();

                var result = new VehicleDTO
                {
                    VehicleId = vehicle.VehicleId,
                    PlateNumber = vehicle.PlateNumber,
                    Model = vehicle.Model,
                    Brand = vehicle.Brand,
                    YearOfManufacture = vehicle.YearOfManufacture,
                    Color = vehicle.Color,
                    PayloadInKg = vehicle.PayloadInKg,
                    VolumeInM3 = vehicle.VolumeInM3,
                    Status = vehicle.Status
                };

                return new ResponseDTO("Create Vehicle Successfully !!!", 200, true, result);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Error while creating Vehicle", 500, false, ex.Message);
            }
        }

        // UPDATE
        public async Task<ResponseDTO> UpdateAsync(VehicleUpdateDTO dto)
        {
            try
            {
                var vehicle = await _unitOfWork.VehicleRepo.GetByIdAsync(dto.VehicleId);
                if (vehicle == null)
                    return new ResponseDTO("Vehicle not found", 404, false);

                vehicle.Model = dto.Model;
                vehicle.Brand = dto.Brand;
                vehicle.Color = dto.Color;
                vehicle.YearOfManufacture = dto.YearOfManufacture;
                vehicle.PayloadInKg = dto.PayloadInKg;
                vehicle.VolumeInM3 = dto.VolumeInM3;
                vehicle.Features = dto.Features ?? new();
                vehicle.CurrentAddress = dto.CurrentAddress;
                vehicle.VehicleTypeId = dto.VehicleTypeId;

                await _unitOfWork.VehicleRepo.UpdateAsync(vehicle);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Vehicle updated successfully", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Error while updating Vehicle", 500, false, ex.Message);
            }
        }

        // SOFT DELETE
        public async Task<ResponseDTO> SoftDeleteAsync(Guid id)
        {
            try
            {
                var vehicle = await _unitOfWork.VehicleRepo.GetByIdAsync(id);
                if (vehicle == null)
                    return new ResponseDTO("Vehicle not found", 404, false);

                vehicle.Status = VehicleStatus.DELETED;
                await _unitOfWork.VehicleRepo.UpdateAsync(vehicle);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Vehicle soft-deleted successfully", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Error while deleting Vehicle", 500, false, ex.Message);
            }
        }

        // GET ALL
        public async Task<ResponseDTO> GetAllAsync()
        {
            var vehicles = await _unitOfWork.VehicleRepo.GetAllAsync(
                filter: v => v.Status != VehicleStatus.DELETED,
                includeProperties: "Owner,VehicleType,VehicleImages"
            );

            var result = vehicles.Select(v => new VehicleDetailDTO
            {
                VehicleId = v.VehicleId,
                PlateNumber = v.PlateNumber,
                Model = v.Model,
                Brand = v.Brand,
                Color = v.Color,
                YearOfManufacture = v.YearOfManufacture,
                PayloadInKg = v.PayloadInKg,
                VolumeInM3 = v.VolumeInM3,
                Status = v.Status,

                // 🧩 Thêm VehicleType object
                VehicleType = new VehicleTypeDTO
                {
                    VehicleTypeId = v.VehicleType.VehicleTypeId,
                    VehicleTypeName = v.VehicleType.VehicleTypeName,
                    Description = v.VehicleType.Description
                },

                // 🧩 Thêm Owner object
                Owner = new GetDetailOwnerDTO
                {
                    UserId = v.Owner.UserId,
                    FullName = v.Owner.FullName,
                    CompanyName = v.Owner.CompanyName
                },

                // 🧩 Thêm danh sách ảnh
                ImageUrls = v.VehicleImages.Select(i => new VehicleImageDetailDTO
                {
                    VehicleImageId = i.VehicleImageId,
                    ImageURL = i.ImageURL,
                    Caption = i.Caption,
                    CreatedAt = i.CreatedAt
                }).ToList()
            }).ToList();

            return new ResponseDTO("Get all vehicles successfully", 200, true, result);
        }


        // GET BY ID
        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO("Unauthorized or invalid token", 401, false);

                var vehicles = await _unitOfWork.VehicleRepo.GetAllAsync(
                    filter: v => v.VehicleId == id && v.Status != VehicleStatus.DELETED,
                    includeProperties: "Owner,VehicleType,VehicleImages,VehicleDocuments"
                );

                var vehicle = vehicles.FirstOrDefault();
                if (vehicle == null)
                    return new ResponseDTO("Vehicle not found", 404, false);

                if (vehicle.OwnerId != userId)
                    return new ResponseDTO("Forbidden: You are not the owner of this vehicle", 403, false);

                var dto = new VehicleDetailDTO
                {
                    VehicleId = vehicle.VehicleId,
                    PlateNumber = vehicle.PlateNumber,
                    Model = vehicle.Model,
                    Brand = vehicle.Brand,
                    Color = vehicle.Color,
                    YearOfManufacture = vehicle.YearOfManufacture,
                    PayloadInKg = vehicle.PayloadInKg,
                    VolumeInM3 = vehicle.VolumeInM3,
                    Status = vehicle.Status,

                    // 🧩 Thông tin loại xe
                    VehicleType = new VehicleTypeDTO
                    {
                        VehicleTypeId = vehicle.VehicleType.VehicleTypeId,
                        VehicleTypeName = vehicle.VehicleType.VehicleTypeName,
                        Description = vehicle.VehicleType.Description
                    },

                    // 🧩 Thông tin chủ xe
                    Owner = new GetDetailOwnerDTO
                    {
                        UserId = vehicle.Owner.UserId,
                        FullName = vehicle.Owner.FullName,
                        Email = vehicle.Owner.Email,
                        PhoneNumber = vehicle.Owner.PhoneNumber,
                        CreatedAt = vehicle.Owner.CreatedAt,
                        LastUpdatedAt = vehicle.Owner.LastUpdatedAt,
                        Status = vehicle.Owner.Status,
                        DateOfBirth = vehicle.Owner.DateOfBirth,
                        AvatarUrl = vehicle.Owner.AvatarUrl,
                        IsEmailVerified = vehicle.Owner.IsEmailVerified,
                        IsPhoneVerified = vehicle.Owner.IsPhoneVerified,
                        Address = vehicle.Owner.Address,
                        TaxCode = vehicle.Owner.TaxCode,
                        BusinessAddress = vehicle.Owner.BusinessAddress,
                        CompanyName = vehicle.Owner.CompanyName,
                        AverageRating = vehicle.Owner.AverageRating
                    },

                    // 🧩 Hình ảnh xe
                    ImageUrls = vehicle.VehicleImages.Select(vi => new VehicleImageDetailDTO
                    {
                        VehicleImageId = vi.VehicleImageId,
                        ImageURL = vi.ImageURL,
                        Caption = vi.Caption,
                        CreatedAt = vi.CreatedAt
                    }).ToList(),

                    // 🧩 Giấy tờ xe
                    Documents = vehicle.VehicleDocuments.Select(doc => new VehicleDocumentDetailDTO
                    {
                        VehicleDocumentId = doc.VehicleDocumentId,
                        DocumentType = doc.DocumentType,
                        FrontDocumentUrl = doc.FrontDocumentUrl,
                        BackDocumentUrl = doc.BackDocumentUrl,
                        ExpirationDate = doc.ExpirationDate,
                        Status = doc.Status,
                        AdminNotes = doc.AdminNotes,
                        CreatedAt = doc.CreatedAt,
                        ProcessedAt = doc.ProcessedAt
                    }).ToList()
                };

                return new ResponseDTO("Get vehicle successfully", 200, true, dto);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error while fetching vehicle: {ex.Message}", 500, false);
            }
        }


    }
}
