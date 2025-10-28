using BLL.Services.Interface;
using Common.DTOs;
using Common.Enums.Status;
using DAL.Entities;
using DAL.Repositories.Interface;
using DAL.UnitOfWork;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using BLL.Utilities;

namespace BLL.Services.Implement
{
    public class VehicleService : IVehicleService
    {
        private readonly IGenericRepository<Vehicle> _vehicleRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;
        public VehicleService(IGenericRepository<Vehicle> vehicleRepository, IUnitOfWork unitOfWork, UserUtility userUtility)
        {
            _vehicleRepository = vehicleRepository;
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
        }

        // CREATE
        public async Task<ResponseDTO> CreateVehicleAsync(VehicleCreateDTO dto)
        {
            var ownerId = _userUtility.GetUserIdFromToken();
            if (ownerId == Guid.Empty)
            {
                return new ResponseDTO { IsSuccess = false, Message = "Unauthorized or invalid token" };
            }

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

            await _vehicleRepository.AddAsync(vehicle);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseDTO
            {
                IsSuccess = true,
                Message = "Vehicle created successfully",
                Result = vehicle.VehicleId
            };
        }

        // UPDATE
        public async Task<ResponseDTO> UpdateVehicleAsync(VehicleUpdateDTO dto)
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(dto.VehicleId);
            if (vehicle == null)
                return new ResponseDTO { IsSuccess = false, Message = "Vehicle not found" };

            vehicle.Model = dto.Model;
            vehicle.Brand = dto.Brand;
            vehicle.Color = dto.Color;
            vehicle.YearOfManufacture = dto.YearOfManufacture;
            vehicle.PayloadInKg = dto.PayloadInKg;
            vehicle.VolumeInM3 = dto.VolumeInM3;
            vehicle.Features = dto.Features ?? new();
            vehicle.CurrentAddress = dto.CurrentAddress;
            vehicle.VehicleTypeId = dto.VehicleTypeId;

            await _vehicleRepository.UpdateAsync(vehicle);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseDTO { IsSuccess = true, Message = "Vehicle updated successfully" };
        }

        // SOFT DELETE
        public async Task<ResponseDTO> SoftDeleteVehicleAsync(Guid id)
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(id);
            if (vehicle == null)
                return new ResponseDTO { IsSuccess = false, Message = "Vehicle not found" };

            vehicle.Status = VehicleStatus.DELETED;
            await _vehicleRepository.UpdateAsync(vehicle);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseDTO { IsSuccess = true, Message = "Vehicle soft-deleted" };
        }

        // GET ALL
        public async Task<ResponseDTO> GetAllVehiclesAsync()
        {
            var vehicles = await _vehicleRepository.GetAllAsync(
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
                VehicleTypeName = v.VehicleType.VehicleTypeName,
                OwnerCompanyName = v.Owner.CompanyName,
                ImageUrls = v.VehicleImages.Select(i => i.ImageURL).ToList()
            }).ToList();

            return new ResponseDTO { IsSuccess = true, Result = result };
        }

        // GET BY ID
        public async Task<ResponseDTO> GetVehicleByIdAsync(Guid id)
        {
            var list = await _vehicleRepository.GetAllAsync(
                filter: v => v.VehicleId == id && v.Status != VehicleStatus.DELETED,
                includeProperties: "Owner,VehicleType,VehicleImages,VehicleDocuments"
            );

            var vehicle = list.FirstOrDefault();
            if (vehicle == null)
                return new ResponseDTO { IsSuccess = false, Message = "Vehicle not found" };

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
                VehicleTypeName = vehicle.VehicleType.VehicleTypeName,
                OwnerCompanyName = vehicle.Owner.CompanyName,
                ImageUrls = vehicle.VehicleImages.Select(i => i.ImageURL).ToList()
            };

            return new ResponseDTO { IsSuccess = true, Result = dto };
        }
    }
}
