using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore; // ⚠️ THÊM USING NÀY
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

        // =========================================================
        // 🔹 1. GET ALL (Cho Admin, có Paging)
        // =========================================================
        public async Task<ResponseDTO> GetAllAsync(int pageNumber, int pageSize)
        {
            try
            {
                // 1. Chỉ Admin
                var adminRole = _userUtility.GetUserRoleFromToken();
                if (adminRole != "Admin")
                {
                    return new ResponseDTO("Forbidden: Access denied", 403, false);
                }

                // 2. Lấy IQueryable (Giả định .GetAll() trả về IQueryable)
                var query = _unitOfWork.BaseUserRepo.GetAll()
                    .AsNoTracking()
                    .Where(u => u.Status != Common.Enums.Status.UserStatus.DELETED)
                    .Include(u => u.Role); // Include Role để lấy RoleName

                // 3. Đếm tổng số
                var totalCount = await query.CountAsync();

                // 4. Lấy dữ liệu của trang
                var users = await query
                    .OrderBy(u => u.FullName)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // 5. Map sang DTO (Dùng lại hàm Map của bạn)
                var dtoList = users.Select(u => MapToBaseProfileDTO(u)).ToList();

                // 6. Trả về
                var paginatedResult = new PaginatedDTO<BaseProfileDTO>(dtoList, totalCount, pageNumber, pageSize);
                return new ResponseDTO("Retrieved all users successfully", 200, true, paginatedResult);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting all users: {ex.Message}", 500, false);
            }
        }


        // =========================================================
        // 🔹 2. GET BY ID (Cho Admin, chi tiết theo Role)
        // =========================================================
        public async Task<ResponseDTO> GetByIdAsync(Guid userId)
        {
            try
            {
                // 1. Chỉ Admin
                var adminRole = _userUtility.GetUserRoleFromToken();
                if (adminRole != "Admin")
                {
                    return new ResponseDTO("Forbidden: Access denied", 403, false);
                }

                // 2. Lấy BaseUser VÀ Role để xác định loại User
                var baseUser = await _unitOfWork.BaseUserRepo.GetAll()
                    .AsNoTracking()
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (baseUser == null)
                {
                    return new ResponseDTO("User not found", 404, false);
                }

                // 3. Dùng switch-case dựa trên vai trò của user ĐƯỢC TRUY VẤN
                object? profileDto = null;
                var roleName = baseUser.Role?.RoleName ?? "Unknown";

                switch (roleName)
                {
                    case "Driver":
                        var driver = await _unitOfWork.DriverRepo.GetAll()
                            .AsNoTracking()
                            .Include(d => d.Role)
                            .Include(d => d.UserDocuments)
                            .Include(d => d.UserViolations)
                            .Include(d => d.Transactions)
                            .Include(d => d.DriverWorkSessions)
                            .Include(d => d.OwnerDriverLinks)
                            .Include(d => d.TripDriverAssignments)
                            .Include(d => d.ActivityLogs)
                            .FirstOrDefaultAsync(d => d.UserId == userId);
                        profileDto = MapToDriverDetailAdminDTO(driver);
                        break;

                    case "Owner":
                        var owner = await _unitOfWork.OwnerRepo.GetAll()
                            .AsNoTracking()
                            .Include(o => o.Role)
                            .Include(o => o.UserDocuments)
                            .Include(o => o.UserViolations)
                            .Include(o => o.Transactions)
                            .Include(o => o.Vehicles)
                            .Include(o => o.Trips)
                            .Include(o => o.OwnerDriverLinks)
                            .FirstOrDefaultAsync(o => o.UserId == userId);
                        profileDto = MapToOwnerDetailAdminDTO(owner);
                        break;

                    case "Provider":
                        var provider = await _unitOfWork.ProviderRepo.GetAll()
                            .AsNoTracking()
                            .Include(p => p.Role)
                            .Include(p => p.UserDocuments)
                            .Include(p => p.UserViolations)
                            .Include(p => p.Transactions)
                            .Include(p => p.Items)
                            .Include(p => p.Packages)
                            .Include(p => p.PostPackages)
                            .Include(p => p.TripProviderContracts)
                            .FirstOrDefaultAsync(p => p.UserId == userId);
                        profileDto = MapToProviderDetailAdminDTO(provider);
                        break;

                    default: // (Admin, Staff,...)
                        var user = await _unitOfWork.BaseUserRepo.GetAll()
                            .AsNoTracking()
                            .Include(u => u.Role)
                            .Include(u => u.UserDocuments)
                            .Include(u => u.UserViolations)
                            .Include(u => u.Transactions)
                            .FirstOrDefaultAsync(u => u.UserId == userId);
                        profileDto = MapToBaseUserDetailAdminDTO(user);
                        break;
                }

                if (profileDto == null)
                    return new ResponseDTO("User profile data not found after query", 404, false);

                return new ResponseDTO("Profile retrieved successfully (Admin)", 200, true, profileDto);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting user by ID: {ex.Message}", 500, false);
            }
        }


        // =========================================================
        // 🔹 3. GET MY PROFILE (Hàm gốc của bạn)
        // =========================================================
        public async Task<ResponseDTO> GetMyProfileAsync()
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO("Unauthorized", 401, false);

                var userRole = _userUtility.GetUserRoleFromToken();
                if (string.IsNullOrEmpty(userRole))
                    return new ResponseDTO("Forbidden: Role not found in token", 403, false);

                object? profileDto = null;

                // (Giả sử các Repo này có hàm Get...ProfileAsync() đã được tối ưu)
                switch (userRole)
                {
                    case "Driver":
                        var driver = await _unitOfWork.DriverRepo.GetDriverProfileAsync(userId);
                        if (driver == null) return new ResponseDTO("Driver profile not found", 404, false);
                        profileDto = MapToDriverProfileDTO(driver);
                        break;

                    case "Owner":
                        var owner = await _unitOfWork.OwnerRepo.GetOwnerProfileAsync(userId);
                        if (owner == null) return new ResponseDTO("Owner profile not found", 404, false);
                        profileDto = MapToOwnerProfileDTO(owner);
                        break;

                    case "Provider":
                        var provider = await _unitOfWork.ProviderRepo.GetProviderProfileAsync(userId);
                        if (provider == null) return new ResponseDTO("Provider profile not found", 404, false);
                        profileDto = MapToProviderProfileDTO(provider);
                        break;

                    default:
                        var baseUser = await _unitOfWork.BaseUserRepo.GetBaseUserByIdAsync(userId);
                        if (baseUser == null) return new ResponseDTO("User profile not found", 404, false);
                        profileDto = MapToBaseProfileDTO(baseUser);
                        break;
                }

                return new ResponseDTO("Profile retrieved successfully", 200, true, profileDto);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error retrieving profile: {ex.Message}", 500, false);
            }
        }

        // =========================================================
        // 🔹 4. CÁC HÀM MAP CHO "GET MY PROFILE" (Hàm gốc của bạn)
        // =========================================================
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
            dto.Role = user.Role?.RoleName ?? "Unknown";
            return dto;
        }

        private DriverProfileDTO MapToDriverProfileDTO(Driver driver)
        {
            var dto = (DriverProfileDTO)MapToBaseProfile(driver, new DriverProfileDTO());
            dto.LicenseNumber = driver.LicenseNumber;
            dto.LicenseClass = driver.LicenseClass;
            dto.LicenseExpiryDate = driver.LicenseExpiryDate;
            dto.IsLicenseVerified = driver.IsLicenseVerified;
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

        // =========================================================
        // 🔹 5. CÁC HÀM MAP MỚI CHO "ADMIN GET BY ID"
        // =========================================================

        private AdminView_BaseUserDTO MapToBaseUserDetailAdminDTO(BaseUser user)
        {
            var dto = (AdminView_BaseUserDTO)MapToBaseProfile(user, new AdminView_BaseUserDTO());

            // Map Collections chung
            dto.UserDocuments = user.UserDocuments?.Select(d => new UserDocumentInDashboardDTO
            {
                UserDocumentId = d.UserDocumentId,
                DocumentType = d.DocumentType.ToString(),
                FrontImageUrl = d.FrontImageUrl,
                BackImageUrl = d.BackImageUrl,
                Status = d.Status.ToString(),
                RejectionReason = d.RejectionReason,
                CreatedAt = d.CreatedAt,
                VerifiedAt = d.VerifiedAt
            }).ToList() ?? new List<UserDocumentInDashboardDTO>();

            dto.UserViolations = user.UserViolations?.Select(v => new UserViolationInDashboardDTO
            {
                UserViolationId = v.UserViolationId,
                TripId = v.TripId,
                Type = v.Type.ToString(),
                Description = v.Description,
                CreateAt = v.CreateAt,
                Status = v.Status.ToString(),
                Severity = v.Severity.ToString()
            }).ToList() ?? new List<UserViolationInDashboardDTO>();

            dto.Transactions = user.Transactions?.Select(t => new TransactionInDashboardDTO
            {
                TransactionId = t.TransactionId,
                Type = t.Type.ToString(),
                Amount = t.Amount,
                Status = t.Status.ToString(),
                //CreateAt = t.CreateAt,
                //PaymentId = t.PaymentId
            }).ToList() ?? new List<TransactionInDashboardDTO>();

            return dto;
        }

        private AdminView_DriverDTO MapToDriverDetailAdminDTO(Driver driver)
        {
            var dto = (AdminView_DriverDTO)MapToBaseUserDetailAdminDTO(driver); // Kế thừa map

            // Map thông tin riêng của Driver
            dto.LicenseNumber = driver.LicenseNumber;
            dto.LicenseClass = driver.LicenseClass;
            dto.LicenseExpiryDate = driver.LicenseExpiryDate;
            dto.IsLicenseVerified = driver.IsLicenseVerified;
            dto.DriverStatus = driver.DriverStatus.ToString();

            // Map Collections của Driver
            dto.DriverWorkSessions = driver.DriverWorkSessions?.Select(ws => new DriverWorkSessionInDashboardDTO
            {
                DriverWorkSessionId = ws.DriverWorkSessionId,
                TripId = ws.TripId,
                StartTime = ws.StartTime,
                EndTime = ws.EndTime,
                Status = ws.Status.ToString()
            }).ToList() ?? new List<DriverWorkSessionInDashboardDTO>();

            dto.OwnerDriverLinks = driver.OwnerDriverLinks?.Select(l => new OwnerDriverLinkInDashboardDTO
            {
                OwnerDriverLinkId = l.OwnerDriverLinkId,
                Status = l.Status.ToString(),
                OwnerId = l.OwnerId,
                DriverId = l.DriverId
            }).ToList() ?? new List<OwnerDriverLinkInDashboardDTO>();

            dto.TripDriverAssignments = driver.TripDriverAssignments?.Select(a => new TripDriverAssignmentInDashboardDTO
            {
                TripDriverAssignmentId = a.TripDriverAssignmentId,
                TripId = a.TripId,
                Type = a.Type.ToString(),
                BaseAmount = a.BaseAmount,
                AssignmentStatus = a.AssignmentStatus.ToString()
            }).ToList() ?? new List<TripDriverAssignmentInDashboardDTO>();

            dto.ActivityLogs = driver.ActivityLogs?.Select(log => new DriverActivityLogInDashboardDTO
            {
                DriverActivityLogId = log.DriverActivityLogId,
                Description = log.Description,
                CreateAt = log.CreateAt
            }).ToList() ?? new List<DriverActivityLogInDashboardDTO>();

            return dto;
        }

        private AdminView_OwnerDTO MapToOwnerDetailAdminDTO(Owner owner)
        {
            var dto = (AdminView_OwnerDTO)MapToBaseUserDetailAdminDTO(owner); // Kế thừa map

            // Map thông tin riêng của Owner
            dto.CompanyName = owner.CompanyName;
            dto.TaxCode = owner.TaxCode;
            dto.AverageRating = owner.AverageRating;

            // Map Collections của Owner
            dto.Vehicles = owner.Vehicles?.Select(v => new VehicleSummaryInDashboardDTO
            {
                VehicleId = v.VehicleId,
                PlateNumber = v.PlateNumber,
                Model = v.Model
            }).ToList() ?? new List<VehicleSummaryInDashboardDTO>();

            dto.Trips = owner.Trips?.Select(t => new TripSummaryInDashboardDTO
            {
                TripId = t.TripId,
                TripCode = t.TripCode,
                Status = t.Status.ToString()
            }).ToList() ?? new List<TripSummaryInDashboardDTO>();

            return dto;
        }

        private AdminView_ProviderDTO MapToProviderDetailAdminDTO(Provider provider)
        {
            var dto = (AdminView_ProviderDTO)MapToBaseUserDetailAdminDTO(provider); // Kế thừa map

            // Map thông tin riêng của Provider
            dto.CompanyName = provider.CompanyName;
            dto.TaxCode = provider.TaxCode;
            dto.AverageRating = provider.AverageRating;

            // Map Collections của Provider
            dto.Items = provider.Items?.Select(i => new ItemSummaryInDashboardDTO
            {
                ItemId = i.ItemId,
                ItemName = i.ItemName
            }).ToList() ?? new List<ItemSummaryInDashboardDTO>();

            dto.Packages = provider.Packages?.Select(p => new PackageSummaryInDashboardDTO
            {
                PackageId = p.PackageId,
                PackageCode = p.PackageCode
            }).ToList() ?? new List<PackageSummaryInDashboardDTO>();

            return dto;
        }
    }
}