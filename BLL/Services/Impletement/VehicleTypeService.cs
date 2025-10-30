using BLL.Services.Interface;
using Common.DTOs;
using DAL.Entities;
using DAL.Repositories.Interface;
using DAL.UnitOfWork;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services.Implement
{
    public class VehicleTypeService : IVehicleTypeService
    {
        private readonly IGenericRepository<VehicleType> _vehicleTypeRepo;
        private readonly IUnitOfWork _unitOfWork;

        public VehicleTypeService(
            IGenericRepository<VehicleType> vehicleTypeRepo,
            IUnitOfWork unitOfWork)
        {
            _vehicleTypeRepo = vehicleTypeRepo;
            _unitOfWork = unitOfWork;
        }

        // CREATE
        public async Task<ResponseDTO> CreateVehicleTypeAsync(VehicleTypeCreateDTO dto)
        {
            var newType = new VehicleType
            {
                VehicleTypeId = Guid.NewGuid(),
                VehicleTypeName = dto.VehicleTypeName,
                Description = dto.Description ?? string.Empty
            };

            await _vehicleTypeRepo.AddAsync(newType);
            await _unitOfWork.SaveChangeAsync();

            var result = new VehicleTypeDTO
            {
                VehicleTypeId = newType.VehicleTypeId,
                VehicleTypeName = newType.VehicleTypeName,
                Description = newType.Description
            };

            return new ResponseDTO
            {
                IsSuccess = true,
                Message = "Vehicle type created successfully",
                Result = result
            };
        }

        // UPDATE
        public async Task<ResponseDTO> UpdateVehicleTypeAsync(VehicleTypeUpdateDTO dto)
        {
            var type = await _vehicleTypeRepo.GetByIdAsync(dto.VehicleTypeId);
            if (type == null)
                return new ResponseDTO { IsSuccess = false, Message = "Vehicle type not found" };

            type.VehicleTypeName = dto.VehicleTypeName;
            type.Description = dto.Description ?? string.Empty;

            await _vehicleTypeRepo.UpdateAsync(type);
            await _unitOfWork.SaveChangeAsync();

            var result = new VehicleTypeDTO
            {
                VehicleTypeId = type.VehicleTypeId,
                VehicleTypeName = type.VehicleTypeName,
                Description = type.Description
            };

            return new ResponseDTO
            {
                IsSuccess = true,
                Message = "Vehicle type updated successfully",
                Result = result
            };
        }

        // DELETE (Soft Delete)
        public async Task<ResponseDTO> SoftDeleteVehicleTypeAsync(Guid id)
        {
            var type = await _vehicleTypeRepo.GetByIdAsync(id);
            if (type == null)
                return new ResponseDTO { IsSuccess = false, Message = "Vehicle type not found" };

            await _vehicleTypeRepo.DeleteAsync(id);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseDTO
            {
                IsSuccess = true,
                Message = "Vehicle type soft deleted successfully"
            };
        }

        // GET ALL
        public async Task<ResponseDTO> GetAllVehicleTypesAsync()
        {
            var types = await _vehicleTypeRepo.GetAllAsync();
            var result = types.Select(t => new VehicleTypeDTO
            {
                VehicleTypeId = t.VehicleTypeId,
                VehicleTypeName = t.VehicleTypeName,
                Description = t.Description
            }).ToList();

            return new ResponseDTO
            {
                IsSuccess = true,
                Message = "Fetched all vehicle types successfully",
                Result = result
            };
        }

        // GET BY ID
        public async Task<ResponseDTO> GetVehicleTypeByIdAsync(Guid id)
        {
            var type = await _vehicleTypeRepo.GetByIdAsync(id);
            if (type == null)
                return new ResponseDTO { IsSuccess = false, Message = "Vehicle type not found" };

            var result = new VehicleTypeDTO
            {
                VehicleTypeId = type.VehicleTypeId,
                VehicleTypeName = type.VehicleTypeName,
                Description = type.Description
            };

            return new ResponseDTO
            {
                IsSuccess = true,
                Message = "Fetched vehicle type successfully",
                Result = result
            };
        }
    }
}
