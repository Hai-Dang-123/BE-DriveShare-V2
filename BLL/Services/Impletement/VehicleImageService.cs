using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Settings;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class VehicleImageService : IVehicleImageService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFirebaseUploadService _firebaseService;
        private readonly UserUtility _userUtility;

        public VehicleImageService(
            IUnitOfWork unitOfWork,
            IFirebaseUploadService firebaseService,
            UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _firebaseService = firebaseService;
            _userUtility = userUtility;
        }

        // CREATE
        public async Task<ResponseDTO> CreateAsync(VehicleImageCreateDTO dto)
        {
            try
            {
                var ownerId = _userUtility.GetUserIdFromToken();
                if (ownerId == Guid.Empty)
                    return new ResponseDTO("Unauthorized or invalid token", 401, false);

                var vehicle = await _unitOfWork.VehicleRepo.GetByIdAsync(dto.VehicleId);
                if (vehicle == null || vehicle.OwnerId != ownerId)
                    return new ResponseDTO("Vehicle not found or not owned by user", 404, false);

                // Upload ảnh lên Firebase
                var imageUrl = await _firebaseService.UploadFileAsync(dto.File, ownerId, FirebaseFileType.VEHICLE_IMAGES);

                var image = new VehicleImage
                {
                    VehicleImageId = Guid.NewGuid(),
                    VehicleId = dto.VehicleId,
                    ImageURL = imageUrl,
                    Caption = dto.Caption,
                    CreatedAt = TimeUtil.NowVN()
                };

                await _unitOfWork.VehicleImageRepo.AddAsync(image);
                await _unitOfWork.SaveChangeAsync();

                var result = new VehicleImageDTO
                {
                    VehicleImageId = image.VehicleImageId,
                    VehicleId = image.VehicleId,
                    Caption = image.Caption,
                    ImageURL = image.ImageURL,
                    CreatedAt = image.CreatedAt
                };

                return new ResponseDTO("Create VehicleImage Successfully !!!", 200, true, result);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Error at saving VehicleImage", 500, false, ex.Message);
            }
        }

        // UPDATE
        public async Task<ResponseDTO> UpdateAsync(VehicleImageUpdateDTO dto)
        {
            try
            {
                var ownerId = _userUtility.GetUserIdFromToken();
                if (ownerId == Guid.Empty)
                    return new ResponseDTO("Unauthorized", 401, false);

                var image = await _unitOfWork.VehicleImageRepo.GetByIdAsync(dto.VehicleImageId);
                if (image == null)
                    return new ResponseDTO("Vehicle image not found", 404, false);

                var vehicle = await _unitOfWork.VehicleRepo.GetByIdAsync(image.VehicleId);
                if (vehicle == null || vehicle.OwnerId != ownerId)
                    return new ResponseDTO("Unauthorized to modify this image", 403, false);

                // Nếu có file mới, upload lại lên Firebase
                if (dto.File != null)
                {
                    var imageUrl = await _firebaseService.UploadFileAsync(dto.File, ownerId, FirebaseFileType.VEHICLE_IMAGES);
                    image.ImageURL = imageUrl;
                }

                image.Caption = dto.Caption ?? image.Caption;

                await _unitOfWork.VehicleImageRepo.UpdateAsync(image);
                await _unitOfWork.SaveChangeAsync();

                var result = new VehicleImageDTO
                {
                    VehicleImageId = image.VehicleImageId,
                    VehicleId = image.VehicleId,
                    Caption = image.Caption,
                    ImageURL = image.ImageURL,
                    CreatedAt = image.CreatedAt
                };

                return new ResponseDTO("Vehicle image updated successfully", 200, true, result);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Error at updating VehicleImage", 500, false, ex.Message);
            }
        }

        // SOFT DELETE
        public async Task<ResponseDTO> SoftDeleteAsync(Guid imageId)
        {
            try
            {
                var ownerId = _userUtility.GetUserIdFromToken();
                if (ownerId == Guid.Empty)
                    return new ResponseDTO("Unauthorized", 401, false);

                var image = await _unitOfWork.VehicleImageRepo.GetByIdAsync(imageId);
                if (image == null)
                    return new ResponseDTO("Vehicle image not found", 404, false);

                var vehicle = await _unitOfWork.VehicleRepo.GetByIdAsync(image.VehicleId);
                if (vehicle == null || vehicle.OwnerId != ownerId)
                    return new ResponseDTO("Unauthorized", 403, false);

                await _unitOfWork.VehicleImageRepo.DeleteAsync(imageId);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Vehicle image deleted successfully", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Error at deleting VehicleImage", 500, false, ex.Message);
            }
        }

        // GET ALL
        public async Task<ResponseDTO> GetAllAsync(Guid vehicleId)
        {
            var ownerId = _userUtility.GetUserIdFromToken();
            if (ownerId == Guid.Empty)
                return new ResponseDTO("Unauthorized", 401, false);

            var vehicle = await _unitOfWork.VehicleRepo.GetByIdAsync(vehicleId);
            if (vehicle == null || vehicle.OwnerId != ownerId)
                return new ResponseDTO("Unauthorized", 403, false);

            var images = await _unitOfWork.VehicleImageRepo.GetAllAsync(v => v.VehicleId == vehicleId);
            var result = images.Select(i => new VehicleImageDTO
            {
                VehicleImageId = i.VehicleImageId,
                VehicleId = i.VehicleId,
                ImageURL = i.ImageURL,
                Caption = i.Caption,
                CreatedAt = i.CreatedAt
            }).ToList();

            return new ResponseDTO("Get all VehicleImages successfully", 200, true, result);
        }

        // GET BY ID
        public async Task<ResponseDTO> GetByIdAsync(Guid imageId)
        {
            var ownerId = _userUtility.GetUserIdFromToken();
            if (ownerId == Guid.Empty)
                return new ResponseDTO("Unauthorized", 401, false);

            var image = await _unitOfWork.VehicleImageRepo.GetByIdAsync(imageId);
            if (image == null)
                return new ResponseDTO("Vehicle image not found", 404, false);

            var vehicle = await _unitOfWork.VehicleRepo.GetByIdAsync(image.VehicleId);
            if (vehicle == null || vehicle.OwnerId != ownerId)
                return new ResponseDTO("Unauthorized", 403, false);

            var dto = new VehicleImageDTO
            {
                VehicleImageId = image.VehicleImageId,
                VehicleId = image.VehicleId,
                ImageURL = image.ImageURL,
                Caption = image.Caption,
                CreatedAt = image.CreatedAt
            };

            return new ResponseDTO("Get VehicleImage successfully", 200, true, dto);
        }

        public async Task AddImagesToVehicleAsync(Guid vehicleId, Guid userId, List<VehicleImageInputDTO> imageDTOs)
        {
            if (imageDTOs == null || !imageDTOs.Any()) return;

            // 1. Tạo danh sách Task để upload song song và trả về Entity hoàn chỉnh
            var tasks = imageDTOs.Select(async dto =>
            {
                // Upload ảnh lên Firebase
                var url = await _firebaseService.UploadFileAsync(dto.ImageFile, userId, FirebaseFileType.VEHICLE_IMAGES);

                // Tạo ngay Entity VehicleImage với thông tin từ DTO
                return new VehicleImage
                {
                    VehicleImageId = Guid.NewGuid(),
                    VehicleId = vehicleId,
                    ImageURL = url,
                    Caption = dto.Caption,

                    // QUAN TRỌNG: Lưu loại ảnh (Toàn cảnh / Biển số)
                    ImageType = dto.ImageType,

                    CreatedAt = TimeUtil.NowVN()
                };
            });

            // 2. Chờ tất cả upload xong
            var vehicleImages = await Task.WhenAll(tasks);

            // 3. Add vào Repo
            if (vehicleImages.Any())
            {
                // Sử dụng AddRange như yêu cầu của bạn
                await _unitOfWork.VehicleImageRepo.AddRangeAsync(vehicleImages);

                // Lưu ý: Hàm SaveChangeAsync thường được gọi ở Service cha (VehicleService) 
                // để đảm bảo Transaction toàn vẹn. Nếu chạy độc lập thì uncomment dòng dưới:
                // await _unitOfWork.SaveChangeAsync();
            }
        }
    }
}
