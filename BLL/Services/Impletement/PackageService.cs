using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
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
    public class PackageService : IPackageService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;
        public PackageService(IUnitOfWork unitOfWork, UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
        }
        // delete package
        public async Task<ResponseDTO> DeletePackageAsync(Guid packageId)
        {
            try
            {
                var package = await _unitOfWork.PackageRepo.GetByIdAsync(packageId);
                if (package == null)
                {
                    return new ResponseDTO
                    {
                        Result = false,
                        StatusCode = StatusCodes.Status404NotFound,
                        Message = "Package not found"
                    };
                }
                package.Status = PackageStatus.Deleted;
                await _unitOfWork.PackageRepo.UpdateAsync(package);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO
                {
                    Result = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Package deleted successfully"
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    Result = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = $"An error occurred: {ex.Message}"
                };
            }
        }
        // get all packages
        public async Task<ResponseDTO> GetAllPackagesAsync()
        {
            try
            {
                var packages = await _unitOfWork.PackageRepo.GetAllPackagesAsync();
                var packageDto = packages.Select(p => new PackageReadDTO
                {
                    PackageId = p.PackageId,
                    PackageCode = p.PackageCode,
                    Title = p.Title,
                    Description = p.Description,
                    Quantity = p.Quantity,
                    Unit = p.Unit,
                    WeightKg = p.WeightKg,
                    VolumeM3 = p.VolumeM3,
                    Status = p.Status,
                    HandlingAttributes = p.HandlingAttributes ?? new List<string>(),
                    OtherRequirements = p.OtherRequirements,
                    OwnerId = p.OwnerId,
                    ProviderId = p.ProviderId,
                    ItemId = p.ItemId,
                    PostPackageId = p.PostPackageId,
                    TripId = p.TripId,
                    //PackageImageUrls = p.PackageImages.Select(i => i.PackageImageURL).ToList(),
                    Item = new ItemReadDTO
                    {
                        Currency = p.Item.Currency,
                        DeclaredValue = p.Item.DeclaredValue,
                        Description = p.Item.Description,
                        ItemId = p.Item.ItemId,
                        ItemName = p.Item.ItemName,
                        OwnerId = p.Item.OwnerId,
                        ProviderId = p.Item.ProviderId,
                        Status = p.Item.Status.ToString(),
                        ImageUrls = p.Item.ItemImages.Select(ii => ii.ItemImageURL).ToList(),
                    },
                    PackageImages = p.PackageImages?.Select(pi => new PackageImageReadDTO
                    {
                        PackageImageId = pi.PackageImageId,
                        PackageId = pi.PackageId,
                        ImageUrl = pi.PackageImageURL,
                        CreatedAt = pi.CreatedAt,
                    }).ToList() ?? new List<PackageImageReadDTO>()
                }).ToList();
           
                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Packages retrieved successfully",
                    Result = packageDto
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    Result = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = $"An error occurred: {ex.Message}"
                };
            }
        }
        // Get package by id
        public async Task<ResponseDTO> GetPackageByIdAsync(Guid packageId)
        {
            try
            {
                var package = await _unitOfWork.PackageRepo.GetPackageByIdAsync(packageId);
                if (package == null)
                {
                    return new ResponseDTO
                    {
                        Result = false,
                        StatusCode = StatusCodes.Status404NotFound,
                        Message = "Package not found"
                    };
                }
                var packageDto = new PackageReadDTO
                {
                    PackageId = package.PackageId,
                    PackageCode = package.PackageCode,
                    Title = package.Title,
                    Description = package.Description,
                    Quantity = package.Quantity,
                    Unit = package.Unit,
                    WeightKg = package.WeightKg,
                    VolumeM3 = package.VolumeM3,
                    Status = package.Status,
                    HandlingAttributes = package.HandlingAttributes ?? new List<string>(),
                    OtherRequirements = package.OtherRequirements,
                    OwnerId = package.OwnerId,
                    ProviderId = package.ProviderId,
                    ItemId = package.ItemId,
                    PostPackageId = package.PostPackageId,
                    TripId = package.TripId,
                    //PackageImageUrls = package.PackageImages.Select(i => i.PackageImageURL).ToList(),
                    Item = new ItemReadDTO
                    {
                        Currency = package.Item.Currency,
                        DeclaredValue = package.Item.DeclaredValue,
                        Description = package.Item.Description,
                        ItemId = package.Item.ItemId,
                        ItemName = package.Item.ItemName,
                        OwnerId = package.Item.OwnerId,
                        ProviderId = package.Item.ProviderId,
                        Status = package.Item.Status.ToString(),
                        ImageUrls = package.Item.ItemImages.Select(ii => ii.ItemImageURL).ToList(),
                    },
                    PackageImages = package.PackageImages?.Select(pi => new PackageImageReadDTO
                    {
                        PackageImageId = pi.PackageImageId,
                        PackageId = pi.PackageId,
                        ImageUrl = pi.PackageImageURL,
                        CreatedAt = pi.CreatedAt,
                    }).ToList() ?? new List<PackageImageReadDTO>()
                };
                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Package retrieved successfully",
                    Result = packageDto
                };

            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    Result = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = $"An error occurred: {ex.Message}"
                };
            }
        }
        // Owner create package
        public async Task<ResponseDTO> OwnerCreatePackageAsync(PackageCreateDTO packageDTO)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO
                    {
                        Result = false,
                        StatusCode = StatusCodes.Status401Unauthorized,
                        Message = "Unauthorized"
                    };

                var package = new Package
                {
                    PackageId = Guid.NewGuid(),
                    PackageCode = packageDTO.PackageCode,
                    Title = packageDTO.Title,
                    Description = packageDTO.Description,
                    Quantity = packageDTO.Quantity,
                    Unit = packageDTO.Unit,
                    WeightKg = packageDTO.WeightKg,
                    VolumeM3 = packageDTO.VolumeM3,
                    HandlingAttributes = packageDTO.HandlingAttributes ?? new(),
                    OtherRequirements = packageDTO.OtherRequirements,
                    OwnerId = userId,
                    ItemId = packageDTO.ItemId,
                    PostPackageId = packageDTO.PostPackageId,
                    TripId = packageDTO.TripId,
                    Status = PackageStatus.Active,
                };

                await _unitOfWork.PackageRepo.AddAsync(package);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO
                {
                    Result = true,
                    StatusCode = StatusCodes.Status201Created,
                    Message = "Package created successfully",
                    
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    Result = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = $"An error occurred: {ex.Message}"
                };
            }
            }
        // Provider create package
        public async Task<ResponseDTO> ProviderCreatePackageAsync(PackageCreateDTO packageDTO)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO
                    {
                        Result = false,
                        StatusCode = StatusCodes.Status401Unauthorized,
                        Message = "Unauthorized"
                    };

                var package = new Package
                {
                    PackageId = Guid.NewGuid(),
                    PackageCode = packageDTO.PackageCode,
                    Title = packageDTO.Title,
                    Description = packageDTO.Description,
                    Quantity = packageDTO.Quantity,
                    Unit = packageDTO.Unit,
                    WeightKg = packageDTO.WeightKg,
                    VolumeM3 = packageDTO.VolumeM3,
                    HandlingAttributes = packageDTO.HandlingAttributes ?? new(),
                    OtherRequirements = packageDTO.OtherRequirements,
                    ProviderId = userId,
                    ItemId = packageDTO.ItemId,
                    PostPackageId = packageDTO.PostPackageId,
                    TripId = packageDTO.TripId,
                    Status = PackageStatus.Active,
                };

                await _unitOfWork.PackageRepo.AddAsync(package);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO
                {
                    Result = true,
                    StatusCode = StatusCodes.Status201Created,
                    Message = "Package created successfully",

                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    Result = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = $"An error occurred: {ex.Message}"
                };
            }
        }
        // Common update package
        public async Task<ResponseDTO> UpdatePackageAsync(PackageUpdateDTO updatePackageDTO)
        {
            var package = await _unitOfWork.PackageRepo.GetByIdAsync(updatePackageDTO.PackageId);
            if (package == null)
                return new ResponseDTO { Result = false, StatusCode = StatusCodes.Status404NotFound, Message = "Package not found" };

            package.Title = updatePackageDTO.Title;
            package.Description = updatePackageDTO.Description;
            package.Quantity = updatePackageDTO.Quantity;
            package.Unit = updatePackageDTO.Unit;
            package.WeightKg = updatePackageDTO.WeightKg;
            package.VolumeM3 = updatePackageDTO.VolumeM3;
            package.HandlingAttributes = updatePackageDTO.HandlingAttributes;
            package.OtherRequirements = updatePackageDTO.OtherRequirements;
          

            await _unitOfWork.PackageRepo.UpdateAsync(package);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseDTO
            {
                Result = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Package updated successfully",
                
            };
        }
    }
}
