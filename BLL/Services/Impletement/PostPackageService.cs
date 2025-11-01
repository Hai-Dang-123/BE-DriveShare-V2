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
    public class PostPackageService : IPostPackageService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;
        public PostPackageService(IUnitOfWork unitOfWork, UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
        }
        // Change Post Package Status
        public async Task<ResponseDTO> ChangePostPackageStatusAsync(ChangePostPackageStatusDTO changePostPackageStatusDTO)
        {
            try
            {
                var postPackage = await _unitOfWork.PostPackageRepo.GetByIdAsync(changePostPackageStatusDTO.PostPackageId);
                if (postPackage == null)
                {
                    return new ResponseDTO
                    {
                        IsSuccess = false,
                        StatusCode = StatusCodes.Status404NotFound,
                        Message = "Post package not found.",
                    };
                }
                postPackage.Status = changePostPackageStatusDTO.NewStatus;
                postPackage.Updated = DateTime.UtcNow;
                await _unitOfWork.PostPackageRepo.UpdateAsync(postPackage);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Post package  Change status successfully.",
                    Result = postPackage,
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = $"An error occurred: {ex.Message}",
                };
            }
        }
        // Create Provider Post Package
        public async Task<ResponseDTO> CreateProviderPostPackageAsync(PostPackageCreateDTO postPackageCreateDTO)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                {
                    return new ResponseDTO
                    {
                        IsSuccess = false,
                        StatusCode = StatusCodes.Status401Unauthorized,
                        Message = "Invalid user token.",
                    };
                }
                var postPackage = new PostPackage
                {
                    PostPackageId = Guid.NewGuid(),
                    ProviderId = userId,
                    Title = postPackageCreateDTO.Title,
                    Description = postPackageCreateDTO.Description,
                    OfferedPrice = postPackageCreateDTO.OfferedPrice,
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow,
                    Status = PostStatus.OPEN,
                    ShippingRouteId = postPackageCreateDTO.ShippingRouteId,
                };
                await _unitOfWork.PostPackageRepo.AddAsync(postPackage);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status201Created,
                    Message = "Post package created successfully.",
                    Result = postPackage,
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = $"An error occurred: {ex.Message}",
                };
            }
            
        }
    }
}
