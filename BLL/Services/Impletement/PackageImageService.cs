using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Settings;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class PackageImageService : IPackageImageService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;
        private readonly IFirebaseUploadService _firebaseService;
        private readonly TimeUtil _timeUtil;
        public PackageImageService(IUnitOfWork unitOfWork, UserUtility userUtility, IFirebaseUploadService firebaseService, TimeUtil timeUtil)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _firebaseService = firebaseService;
            _timeUtil = timeUtil;
        }
        // CREATE PACKAGE IMAGE
        public async Task<ResponseDTO> CreatePackageImageAsync(PackageImageCreateDTO packageImageCreateDTO)
        {
            try
            { 
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                {
                    return new ResponseDTO
                    {
                        Result = false,
                        StatusCode = StatusCodes.Status401Unauthorized,
                        Message = "Unauthorized access."
                    };
                }
                var package = await _unitOfWork.PackageRepo.GetByIdAsync(packageImageCreateDTO.PackageId);
                if (package == null)
                {
                    return new ResponseDTO
                    {
                        Result = false,
                        StatusCode = StatusCodes.Status404NotFound,
                        Message = "Package not found."
                    };
                }
                var img = await _firebaseService.UploadFileAsync(packageImageCreateDTO.File, userId, FirebaseFileType.PACKAGE_IMAGES);
                var newImage = new PackageImage
                {
                    PackageImageId = Guid.NewGuid(),
                    PackageId = packageImageCreateDTO.PackageId,
                    PackageImageURL = img,
                    CreatedAt = TimeUtil.NowVN(),
                    Status = PackageImageStatus.Active
                };

                await _unitOfWork.PackageImageRepo.AddAsync(newImage);
                await _unitOfWork.SaveAsync();

                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status201Created,
                    Message = "Package image created successfully.",
                    
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    Result = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = $"Error: {ex.Message}"
                };
            }
        }
        // DELETE PACKAGE IMAGE
        public async Task<ResponseDTO> DeletePackageImageAsync(Guid packageImageId)
        {
            try
            {
                var image = await _unitOfWork.PackageImageRepo.GetByIdAsync(packageImageId);
                if (image == null)
                {
                    return new ResponseDTO
                    {
                        Result = false,
                        StatusCode = StatusCodes.Status404NotFound,
                        Message = "Package image not found."
                    };
                }
                image.Status = PackageImageStatus.Deleted;
                await _unitOfWork.PackageImageRepo.UpdateAsync(image);
                await _unitOfWork.SaveAsync();

                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Package image deleted successfully."
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    Result = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = $"Error: {ex.Message}"
                };
            }
        }
        // GET ALL PACKAGE IMAGES BY PACKAGE ID
        public async Task<ResponseDTO> GetAllPackageImagesByPackageIdAsync(Guid packageId)
        {
            try
            {
                var images = await _unitOfWork.PackageImageRepo.GetAllByPackageIdAsync(packageId);

                if (images == null || !images.Any())
                {
                    return new ResponseDTO
                    {
                        Result = false,
                        StatusCode = StatusCodes.Status404NotFound,
                        Message = "No package images found for this package."
                    };
                }

                var imageDtos = images.Select(img => new PackageImageReadDTO
                {
                    PackageImageId = img.PackageImageId,
                    PackageId = img.PackageId,
                    ImageUrl = img.PackageImageURL,
                    CreatedAt = img.CreatedAt
                }).ToList();

                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Package images retrieved successfully.",
                    Result = imageDtos
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    Result = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = $"Error: {ex.Message}"
                };
            }
        }
        // GET PACKAGE IMAGE BY ID
        public async Task<ResponseDTO> GetPackageImageByIdAsync(Guid packageImageId)
        {
            try
            {
                var image = await _unitOfWork.PackageImageRepo.GetByIdAsync(packageImageId);

                if (image == null)
                {
                    return new ResponseDTO
                    {
                        Result = false,
                        StatusCode = StatusCodes.Status404NotFound,
                        Message = "Package image not found."
                    };
                }

                var dto = new PackageImageReadDTO
                {
                    PackageImageId = image.PackageImageId,
                    PackageId = image.PackageId,
                    ImageUrl = image.PackageImageURL,
                    CreatedAt = image.CreatedAt
                };

                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Package image retrieved successfully.",
                    Result = dto
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    Result = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = $"Error: {ex.Message}"
                };
            }
        }
        // UPDATE PACKAGE IMAGE
        public async Task<ResponseDTO> UpdatePackageImageAsync(UpdatePackageImageDTO updatePackageImageDTO)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                {
                    return new ResponseDTO
                    {
                        Result = false,
                        StatusCode = StatusCodes.Status401Unauthorized,
                        Message = "Unauthorized access."
                    };
                }
                var image = await _unitOfWork.PackageImageRepo.GetByIdAsync(updatePackageImageDTO.PackageId);
                if (image == null)
                {
                    return new ResponseDTO
                    {
                        Result = false,
                        StatusCode = StatusCodes.Status404NotFound,
                        Message = "Package image not found."
                    };
                }

                if (updatePackageImageDTO.File != null)
                {
                    var imageUrl = await _firebaseService.UploadFileAsync(updatePackageImageDTO.File, userId, FirebaseFileType.PACKAGE_IMAGES);

                    image.PackageImageURL = imageUrl;
                }
                 image.PackageId = updatePackageImageDTO.PackageId;
                

                await _unitOfWork.PackageImageRepo.UpdateAsync(image);
                await _unitOfWork.SaveAsync();

                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Package image updated successfully.",     
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    Result = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        public async Task AddImagesToPackageAsync(Guid packageId, Guid userId, List<IFormFile> files)
        {
            if (files == null || !files.Any())
            {
                return; // Không có file, bỏ qua
            }

            // 1. Upload song song tất cả file
            var uploadTasks = files
                .Where(f => f != null && f.Length > 0)
                .Select(file => _firebaseService.UploadFileAsync(file, userId, FirebaseFileType.PACKAGE_IMAGES))
                .ToList();

            var imageUrls = await Task.WhenAll(uploadTasks);

            // 2. Thêm tất cả ảnh vào UnitOfWork
            foreach (var url in imageUrls)
            {
                var newImage = new PackageImage
                {
                    PackageImageId = Guid.NewGuid(),
                    PackageId = packageId,
                    PackageImageURL = url,
                    CreatedAt = TimeUtil.NowVN(),
                    Status = PackageImageStatus.Active
                };

                // Chỉ Add, KHÔNG Save
                await _unitOfWork.PackageImageRepo.AddAsync(newImage);
            }
        }
    }
}
