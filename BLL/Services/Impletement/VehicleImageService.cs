using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Settings;
using DAL.Entities;
using DAL.Repositories.Interface;
using DAL.UnitOfWork;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services.Implement
{
    public class VehicleImageService : IVehicleImageService
    {
        private readonly IGenericRepository<VehicleImage> _vehicleImageRepo;
        private readonly IGenericRepository<Vehicle> _vehicleRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFirebaseUploadService _firebaseService;
        private readonly UserUtility _userUtility;

        public VehicleImageService(
            IGenericRepository<VehicleImage> vehicleImageRepo,
            IGenericRepository<Vehicle> vehicleRepo,
            IUnitOfWork unitOfWork,
            IFirebaseUploadService firebaseService,
            UserUtility userUtility)
        {
            _vehicleImageRepo = vehicleImageRepo;
            _vehicleRepo = vehicleRepo;
            _unitOfWork = unitOfWork;
            _firebaseService = firebaseService;
            _userUtility = userUtility;
        }

        // CREATE
        public async Task<ResponseDTO> CreateVehicleImageAsync(VehicleImageCreateDTO dto)
        {
            var ownerId = _userUtility.GetUserIdFromToken();
            if (ownerId == Guid.Empty)
                return new ResponseDTO { IsSuccess = false, Message = "Unauthorized or invalid token" };

            var vehicle = await _vehicleRepo.GetByIdAsync(dto.VehicleId);
            if (vehicle == null || vehicle.OwnerId != ownerId)
                return new ResponseDTO { IsSuccess = false, Message = "Vehicle not found or not owned by user" };

            // Upload ảnh lên Firebase
            var imageUrl = await _firebaseService.UploadFileAsync(dto.File, ownerId, FirebaseFileType.VEHICLE_IMAGES);

            var image = new VehicleImage
            {
                VehicleImageId = Guid.NewGuid(),
                VehicleId = dto.VehicleId,
                ImageURL = imageUrl,
                Caption = dto.Caption,
                CreatedAt = DateTime.UtcNow
            };

            await _vehicleImageRepo.AddAsync(image);
            await _unitOfWork.SaveChangeAsync();

            var dtoResult = new VehicleImageDTO
            {
                VehicleImageId = image.VehicleImageId,
                VehicleId = image.VehicleId,
                Caption = image.Caption,
                ImageURL = image.ImageURL,
                CreatedAt = image.CreatedAt
            };

            return new ResponseDTO
            {
                IsSuccess = true,
                Message = "Vehicle image uploaded successfully",
                Result = dtoResult
            };

        }

        // UPDATE
        public async Task<ResponseDTO> UpdateVehicleImageAsync(VehicleImageUpdateDTO dto)
        {
            var ownerId = _userUtility.GetUserIdFromToken();
            if (ownerId == Guid.Empty)
                return new ResponseDTO { IsSuccess = false, Message = "Unauthorized" };

            var image = await _vehicleImageRepo.GetByIdAsync(dto.VehicleImageId);
            if (image == null)
                return new ResponseDTO { IsSuccess = false, Message = "Vehicle image not found" };

            var vehicle = await _vehicleRepo.GetByIdAsync(image.VehicleId);
            if (vehicle == null || vehicle.OwnerId != ownerId)
                return new ResponseDTO { IsSuccess = false, Message = "Unauthorized to modify this image" };

            if (dto.File != null)
            {
                var imageUrl = await _firebaseService.UploadFileAsync(dto.File, ownerId, FirebaseFileType.VEHICLE_IMAGES);

                image.ImageURL = imageUrl;
            }

            image.Caption = dto.Caption ?? image.Caption;

            await _vehicleImageRepo.UpdateAsync(image);
            await _unitOfWork.SaveChangeAsync();

            var dtoResult = new VehicleImageDTO
            {
                VehicleImageId = image.VehicleImageId,
                VehicleId = image.VehicleId,
                Caption = image.Caption,
                ImageURL = image.ImageURL,
                CreatedAt = image.CreatedAt
            };

            return new ResponseDTO
            {
                IsSuccess = true,
                Message = "Vehicle image updated successfully",
                Result = dtoResult
            };

        }

        // SOFT DELETE
        public async Task<ResponseDTO> SoftDeleteVehicleImageAsync(Guid imageId)
        {
            var ownerId = _userUtility.GetUserIdFromToken();
            if (ownerId == Guid.Empty)
                return new ResponseDTO { IsSuccess = false, Message = "Unauthorized" };

            var image = await _vehicleImageRepo.GetByIdAsync(imageId);
            if (image == null)
                return new ResponseDTO { IsSuccess = false, Message = "Vehicle image not found" };

            var vehicle = await _vehicleRepo.GetByIdAsync(image.VehicleId);
            if (vehicle == null || vehicle.OwnerId != ownerId)
                return new ResponseDTO { IsSuccess = false, Message = "Unauthorized" };

            await _vehicleImageRepo.DeleteAsync(imageId);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseDTO { IsSuccess = true, Message = "Vehicle image deleted successfully" };
        }

        // GET ALL
        public async Task<ResponseDTO> GetAllVehicleImagesAsync(Guid vehicleId)
        {
            var ownerId = _userUtility.GetUserIdFromToken();
            if (ownerId == Guid.Empty)
                return new ResponseDTO { IsSuccess = false, Message = "Unauthorized" };

            var vehicle = await _vehicleRepo.GetByIdAsync(vehicleId);
            if (vehicle == null || vehicle.OwnerId != ownerId)
                return new ResponseDTO { IsSuccess = false, Message = "Unauthorized" };

            var images = await _vehicleImageRepo.GetAllAsync(v => v.VehicleId == vehicleId);
            var result = images.Select(i => new VehicleImageDetailDTO
            {
                VehicleImageId = i.VehicleImageId,
                VehicleId = i.VehicleId,
                ImageURL = i.ImageURL,
                Caption = i.Caption,
                CreatedAt = i.CreatedAt
            }).ToList();

            return new ResponseDTO { IsSuccess = true, Result = result };
        }

        // GET BY ID
        public async Task<ResponseDTO> GetVehicleImageByIdAsync(Guid imageId)
        {
            var ownerId = _userUtility.GetUserIdFromToken();
            if (ownerId == Guid.Empty)
                return new ResponseDTO { IsSuccess = false, Message = "Unauthorized" };

            var image = await _vehicleImageRepo.GetByIdAsync(imageId);
            if (image == null)
                return new ResponseDTO { IsSuccess = false, Message = "Vehicle image not found" };

            var vehicle = await _vehicleRepo.GetByIdAsync(image.VehicleId);
            if (vehicle == null || vehicle.OwnerId != ownerId)
                return new ResponseDTO { IsSuccess = false, Message = "Unauthorized" };

            var dto = new VehicleImageDetailDTO
            {
                VehicleImageId = image.VehicleImageId,
                VehicleId = image.VehicleId,
                ImageURL = image.ImageURL,
                Caption = image.Caption,
                CreatedAt = image.CreatedAt
            };

            return new ResponseDTO { IsSuccess = true, Result = dto };
        }
    }
}
