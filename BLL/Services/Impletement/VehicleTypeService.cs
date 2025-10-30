using BLL.Services.Interface;
using Common.DTOs;
using DAL.Entities;
using DAL.UnitOfWork;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class VehicleTypeService : IVehicleTypeService
    {
        private readonly IUnitOfWork _unitOfWork;

        public VehicleTypeService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // CREATE
        public async Task<ResponseDTO> CreateAsync(VehicleTypeCreateDTO dto)
        {
            try
            {
                var newType = new VehicleType
                {
                    VehicleTypeId = Guid.NewGuid(),
                    VehicleTypeName = dto.VehicleTypeName,
                    Description = dto.Description ?? string.Empty
                };

                await _unitOfWork.VehicleTypeRepo.AddAsync(newType);
                await _unitOfWork.SaveChangeAsync();

                var result = new VehicleTypeDTO
                {
                    VehicleTypeId = newType.VehicleTypeId,
                    VehicleTypeName = newType.VehicleTypeName,
                    Description = newType.Description
                };

                return new ResponseDTO("Create VehicleType Successfully !!!", 200, true, result);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Error while creating VehicleType", 500, false, ex.Message);
            }
        }

        // UPDATE
        public async Task<ResponseDTO> UpdateAsync(VehicleTypeUpdateDTO dto)
        {
            try
            {
                var type = await _unitOfWork.VehicleTypeRepo.GetByIdAsync(dto.VehicleTypeId);
                if (type == null)
                    return new ResponseDTO("VehicleType not found", 404, false);

                type.VehicleTypeName = dto.VehicleTypeName;
                type.Description = dto.Description ?? string.Empty;

                await _unitOfWork.VehicleTypeRepo.UpdateAsync(type);
                await _unitOfWork.SaveChangeAsync();

                var result = new VehicleTypeDTO
                {
                    VehicleTypeId = type.VehicleTypeId,
                    VehicleTypeName = type.VehicleTypeName,
                    Description = type.Description
                };

                return new ResponseDTO("VehicleType updated successfully", 200, true, result);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Error while updating VehicleType", 500, false, ex.Message);
            }
        }

        // SOFT DELETE
        public async Task<ResponseDTO> SoftDeleteAsync(Guid id)
        {
            try
            {
                var type = await _unitOfWork.VehicleTypeRepo.GetByIdAsync(id);
                if (type == null)
                    return new ResponseDTO("VehicleType not found", 404, false);

                await _unitOfWork.VehicleTypeRepo.DeleteAsync(id);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("VehicleType soft-deleted successfully", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Error while deleting VehicleType", 500, false, ex.Message);
            }
        }

        // GET ALL
        public async Task<ResponseDTO> GetAllAsync()
        {
            try
            {
                var types = await _unitOfWork.VehicleTypeRepo.GetAllAsync();
                var result = types.Select(t => new VehicleTypeDTO
                {
                    VehicleTypeId = t.VehicleTypeId,
                    VehicleTypeName = t.VehicleTypeName,
                    Description = t.Description
                }).ToList();

                return new ResponseDTO("Get all VehicleTypes successfully", 200, true, result);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Error while fetching VehicleTypes", 500, false, ex.Message);
            }
        }

        // GET BY ID
        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            try
            {
                var type = await _unitOfWork.VehicleTypeRepo.GetByIdAsync(id);
                if (type == null)
                    return new ResponseDTO("VehicleType not found", 404, false);

                var dto = new VehicleTypeDTO
                {
                    VehicleTypeId = type.VehicleTypeId,
                    VehicleTypeName = type.VehicleTypeName,
                    Description = type.Description
                };

                return new ResponseDTO("Get VehicleType successfully", 200, true, dto);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Error while fetching VehicleType", 500, false, ex.Message);
            }
        }
    }
}
