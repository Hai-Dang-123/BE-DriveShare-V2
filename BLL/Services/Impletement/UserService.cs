using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
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
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;

        public UserService(IUnitOfWork unitOfWork, UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
        }

        public async Task<ResponseDTO> GetMyProfileAsync()
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO { /* Unauthorized 401 */ };

                // BẮT BUỘC: Bạn cần lấy được vai trò (Role) từ token
                // Giả sử _userUtility có phương thức GetUserRoleFromToken()
                var userRole = _userUtility.GetUserRoleFromToken();
                if (string.IsNullOrEmpty(userRole))
                    return new ResponseDTO { /* Forbidden 403 */ };

                object? profileDto = null;

                // Dùng switch-case dựa trên vai trò lấy từ token
                switch (userRole)
                {
                    case "Driver":
                        var driver = await _unitOfWork.DriverRepo.GetDriverProfileAsync(userId);
                        if (driver == null) return new ResponseDTO { /* Not Found 404 */ };
                        profileDto = MapToDriverProfileDTO(driver); // Map thủ công
                        break;

                    case "Owner":
                        var owner = await _unitOfWork.OwnerRepo.GetOwnerProfileAsync(userId);
                        if (owner == null) return new ResponseDTO { /* Not Found 404 */ };
                        profileDto = MapToOwnerProfileDTO(owner);
                        break;

                    case "Provider":
                        var provider = await _unitOfWork.ProviderRepo.GetProviderProfileAsync(userId);
                        if (provider == null) return new ResponseDTO { /* Not Found 404 */ };
                        profileDto = MapToProviderProfileDTO(provider);
                        break;

                    default:
                        // Xử lý các vai trò khác (ví dụ: Admin, Staff)
                        var baseUser = await _unitOfWork.BaseUserRepo.GetBaseUserByIdAsync(userId);
                        if (baseUser == null) return new ResponseDTO { /* Not Found 404 */ };
                        profileDto = MapToBaseProfileDTO(baseUser);
                        break;
                }

                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Profile retrieved successfully",
                    Result = profileDto
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO { /* Internal Server Error 500 */ };
            }
        }
        private BaseProfileDTO MapToBaseProfile(BaseUser user, BaseProfileDTO dto)
        {
            dto.FullName = user.FullName;
            dto.Email = user.Email;
            dto.PhoneNumber = user.PhoneNumber;
            dto.Status = user.Status.ToString();
            dto.DateOfBirth = user.DateOfBirth;
            dto.AvatarUrl = user.AvatarUrl;
            dto.IsEmailVerified = user.IsEmailVerified;
            dto.IsPhoneVerified = user.IsPhoneVerified;
            dto.Address = user.Address;
            dto.Role = user.Role?.RoleName ?? "Unknown"; // Giả sử có RoleName
            return dto;
        }

        private DriverProfileDTO MapToDriverProfileDTO(Driver driver)
        {
            var dto = (DriverProfileDTO)MapToBaseProfile(driver, new DriverProfileDTO());
            dto.LicenseNumber = driver.LicenseNumber;
            dto.LicenseClass = driver.LicenseClass;
            dto.LicenseExpiryDate = driver.LicenseExpiryDate;
            dto.IsLicenseVerified = driver.IsLicenseVerified;
            //dto.IsInTrip = driver.IsInTrip;
            dto.TotalTripsAssigned = driver.TripDriverAssignments?.Count ?? 0;
            dto.LinkedOwnersCount = driver.OwnerDriverLinks?.Count ?? 0;
            return dto;
        }

        private OwnerProfileDTO MapToOwnerProfileDTO(Owner owner)
        {
            var dto = (OwnerProfileDTO)MapToBaseProfile(owner, new OwnerProfileDTO());
            dto.CompanyName = owner.CompanyName;
            dto.TaxCode = owner.TaxCode;
            dto.BusinessAddress = owner.BusinessAddress;
            dto.AverageRating = owner.AverageRating;
            dto.TotalVehicles = owner.Vehicles?.Count ?? 0;
            dto.TotalDrivers = owner.OwnerDriverLinks?.Count ?? 0;
            dto.TotalTripsCreated = owner.Trips?.Count ?? 0;
            return dto;
        }

        private ProviderProfileDTO MapToProviderProfileDTO(Provider provider)
        {
            var dto = (ProviderProfileDTO)MapToBaseProfile(provider, new ProviderProfileDTO());
            dto.CompanyName = provider.CompanyName;
            dto.TaxCode = provider.TaxCode;
            dto.BusinessAddress = provider.BusinessAddress;
            dto.AverageRating = provider.AverageRating;
            dto.TotalItems = provider.Items?.Count ?? 0;
            dto.TotalPackages = provider.Packages?.Count ?? 0;
            dto.TotalPackagePosts = provider.PostPackages?.Count ?? 0;
            return dto;
        }

        private BaseProfileDTO MapToBaseProfileDTO(BaseUser user)
        {
            return MapToBaseProfile(user, new BaseProfileDTO());
        }
    }
}
