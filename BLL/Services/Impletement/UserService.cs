using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
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
        public async Task<ResponseDTO> GetAllAsync(
       int pageNumber,
       int pageSize,
       string search = null,
       string sortField = null,
       string sortDirection = "ASC"
   )
        {
            try
            {
                var allowedRoles = new[] { "Provider", "Driver", "Owner" };

                var query = _unitOfWork.BaseUserRepo.GetAll()
                    .AsNoTracking()
                    .Include(u => u.Role)
                    .Include(u => u.UserDocuments)
                    .Where(u =>
                        u.Status != Common.Enums.Status.UserStatus.DELETED &&
                        allowedRoles.Contains(u.Role.RoleName)
                    );

                // ================================
                // 1️⃣ SEARCH
                // ================================
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var keyword = search.Trim().ToLower();

                    query = query.Where(u =>
                        (u.FullName != null && u.FullName.ToLower().Contains(keyword)) ||
                        (u.Email != null && u.Email.ToLower().Contains(keyword)) ||
                        (u.PhoneNumber != null && u.PhoneNumber.Contains(keyword))
                    );
                }

                // ================================
                // 2️⃣ SORT
                // ================================
                bool desc = sortDirection?.ToUpper() == "DESC";

                query = sortField?.ToLower() switch
                {
                    "fullname" => desc ? query.OrderByDescending(u => u.FullName)
                                       : query.OrderBy(u => u.FullName),

                    "email" => desc ? query.OrderByDescending(u => u.Email)
                                    : query.OrderBy(u => u.Email),

                    "createdat" => desc ? query.OrderByDescending(u => u.CreatedAt)
                                        : query.OrderBy(u => u.CreatedAt),

                    "role" => desc ? query.OrderByDescending(u => u.Role.RoleName)
                                   : query.OrderBy(u => u.Role.RoleName),

                    _ => query.OrderBy(u => u.FullName)   // Mặc định
                };

                // ================================
                // 3️⃣ PAGING
                // ================================
                var totalCount = await query.CountAsync();

                var users = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var dtoList = users.Select(u => MapToBaseProfileDTO(u)).ToList();

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
            dto.UserId = user.UserId;
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

            // --- [LOGIC MỚI] CHECK CCCD (Citizen ID) ---
            // Kiểm tra trong danh sách UserDocuments xem có loại CCCD và Status là Verified chưa
            if (user.UserDocuments != null && user.UserDocuments.Any())
            {
                if (user.UserDocuments.Any(d => d.Status == VerifileStatus.PENDING_REVIEW))
                {
                    dto.DocumentStatus = "PENDING_REVIEW";
                }
                else if (user.UserDocuments.Any(d => d.Status == VerifileStatus.REJECTED))
                {
                    dto.DocumentStatus = "REJECTED";
                }
                else if (user.UserDocuments.Any(d => d.Status == VerifileStatus.ACTIVE))
                {
                    dto.DocumentStatus = "ACTIVE";
                }
                else
                {
                    dto.DocumentStatus = "INACTIVE";
                }

                dto.HasPendingDocumentRequest =
                    user.UserDocuments.Any(d => d.Status == VerifileStatus.PENDING_REVIEW);
            }
            else
            {
                dto.DocumentStatus = "NONE";
                dto.HasPendingDocumentRequest = false;
            }


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

            // --- [LOGIC MỚI] CHECK GPLX (Driver License) ---
            if (driver.UserDocuments != null)
            {
                // 1. Check GPLX (Driver License)
                var gplx = driver.UserDocuments.FirstOrDefault(d => d.DocumentType == DocumentType.DRIVER_LINCENSE);
                dto.HasVerifiedDriverLicense = (gplx != null && gplx.Status == VerifileStatus.ACTIVE);

                // 2. [MỚI] Check GKSK (Health Check)
                var gksk = driver.UserDocuments.FirstOrDefault(d => d.DocumentType == DocumentType.HEALTH_CHECK);
                dto.HasVerifiedHealthCheck = (gksk != null && gksk.Status == VerifileStatus.ACTIVE);
            }
            else
            {
                // Nếu list null hoặc rỗng -> Chưa có gì cả
                dto.HasVerifiedDriverLicense = false;
                dto.HasVerifiedHealthCheck = false;
            }

            // --- [LOGIC MỚI] CHECK LỊCH SỬ CHẠY XE ---
            // Lấy giá trị từ cột HasDeclaredInitialHistory (đã tạo ở bước trước)
            dto.HasDeclaredInitialHistory = driver.HasDeclaredInitialHistory;

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

        // =========================================================
        // 🔹 6. GET ALL USER BY SPECIFIC ROLE (Có Paging, Sort, Search chi tiết)
        // =========================================================
        public async Task<ResponseDTO> GetAllUserByRoleAsync(
            string roleName,
            int pageNumber,
            int pageSize,
            string search = null,
            string sortField = null,
            string sortDirection = "ASC")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roleName))
                    return new ResponseDTO("Role name is required.", 400, false);

                roleName = roleName.Trim();
                bool desc = sortDirection?.ToUpper() == "DESC";
                string keyword = search?.Trim().ToLower();

                // ---------------------------------------------------------
                // CASE 1: DRIVER
                // ---------------------------------------------------------
                if (roleName.Equals("Driver", StringComparison.OrdinalIgnoreCase))
                {
                    var query = _unitOfWork.DriverRepo.GetAll()
                        .AsNoTracking()
                        .Include(u => u.Role)
                        .Include(u => u.UserDocuments) // Cần để map Verified Status
                        .Where(u => u.Status != UserStatus.DELETED);

                    // Search
                    if (!string.IsNullOrEmpty(keyword))
                    {
                        query = query.Where(u =>
                            u.FullName.ToLower().Contains(keyword) ||
                            u.Email.ToLower().Contains(keyword) ||
                            u.PhoneNumber.Contains(keyword) ||
                            (u.LicenseNumber != null && u.LicenseNumber.ToLower().Contains(keyword)) // Search riêng Driver
                        );
                    }

                    // Sort
                    query = sortField?.ToLower() switch
                    {
                        "fullname" => desc ? query.OrderByDescending(u => u.FullName) : query.OrderBy(u => u.FullName),
                        "email" => desc ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
                        "createdat" => desc ? query.OrderByDescending(u => u.CreatedAt) : query.OrderBy(u => u.CreatedAt),
                        "licensenumber" => desc ? query.OrderByDescending(u => u.LicenseNumber) : query.OrderBy(u => u.LicenseNumber),
                        _ => query.OrderByDescending(u => u.CreatedAt)
                    };

                    // Paging
                    var totalCount = await query.CountAsync();
                    var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

                    // Mapping
                    var dtos = items.Select(x => MapToDriverProfileDTO(x)).ToList();
                    return new ResponseDTO("Success", 200, true, new PaginatedDTO<DriverProfileDTO>(dtos, totalCount, pageNumber, pageSize));
                }

                // ---------------------------------------------------------
                // CASE 2: OWNER
                // ---------------------------------------------------------
                else if (roleName.Equals("Owner", StringComparison.OrdinalIgnoreCase))
                {
                    var query = _unitOfWork.OwnerRepo.GetAll()
                        .AsNoTracking()
                        .Include(u => u.Role)
                        .Include(u => u.UserDocuments)
                        .Where(u => u.Status != UserStatus.DELETED);

                    // Search
                    if (!string.IsNullOrEmpty(keyword))
                    {
                        query = query.Where(u =>
                            u.FullName.ToLower().Contains(keyword) ||
                            u.Email.ToLower().Contains(keyword) ||
                            u.PhoneNumber.Contains(keyword) ||
                            (u.CompanyName != null && u.CompanyName.ToLower().Contains(keyword)) || // Search riêng Owner
                            (u.TaxCode != null && u.TaxCode.Contains(keyword))
                        );
                    }

                    // Sort
                    query = sortField?.ToLower() switch
                    {
                        "fullname" => desc ? query.OrderByDescending(u => u.FullName) : query.OrderBy(u => u.FullName),
                        "email" => desc ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
                        "createdat" => desc ? query.OrderByDescending(u => u.CreatedAt) : query.OrderBy(u => u.CreatedAt),
                        "companyname" => desc ? query.OrderByDescending(u => u.CompanyName) : query.OrderBy(u => u.CompanyName),
                        _ => query.OrderByDescending(u => u.CreatedAt)
                    };

                    // Paging
                    var totalCount = await query.CountAsync();
                    var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

                    // Mapping
                    var dtos = items.Select(x => MapToOwnerProfileDTO(x)).ToList();
                    return new ResponseDTO("Success", 200, true, new PaginatedDTO<OwnerProfileDTO>(dtos, totalCount, pageNumber, pageSize));
                }

                // ---------------------------------------------------------
                // CASE 3: PROVIDER
                // ---------------------------------------------------------
                else if (roleName.Equals("Provider", StringComparison.OrdinalIgnoreCase))
                {
                    var query = _unitOfWork.ProviderRepo.GetAll()
                        .AsNoTracking()
                        .Include(u => u.Role)
                        .Include(u => u.UserDocuments)
                        .Where(u => u.Status != UserStatus.DELETED);

                    // Search
                    if (!string.IsNullOrEmpty(keyword))
                    {
                        query = query.Where(u =>
                            u.FullName.ToLower().Contains(keyword) ||
                            u.Email.ToLower().Contains(keyword) ||
                            u.PhoneNumber.Contains(keyword) ||
                            (u.CompanyName != null && u.CompanyName.ToLower().Contains(keyword)) // Search riêng Provider
                        );
                    }

                    // Sort
                    query = sortField?.ToLower() switch
                    {
                        "fullname" => desc ? query.OrderByDescending(u => u.FullName) : query.OrderBy(u => u.FullName),
                        "email" => desc ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
                        "createdat" => desc ? query.OrderByDescending(u => u.CreatedAt) : query.OrderBy(u => u.CreatedAt),
                        "companyname" => desc ? query.OrderByDescending(u => u.CompanyName) : query.OrderBy(u => u.CompanyName),
                        _ => query.OrderByDescending(u => u.CreatedAt)
                    };

                    // Paging
                    var totalCount = await query.CountAsync();
                    var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

                    // Mapping
                    var dtos = items.Select(x => MapToProviderProfileDTO(x)).ToList();
                    return new ResponseDTO("Success", 200, true, new PaginatedDTO<ProviderProfileDTO>(dtos, totalCount, pageNumber, pageSize));
                }
                else
                {
                    return new ResponseDTO($"Role '{roleName}' is not supported for this API.", 400, false);
                }
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting users by role: {ex.Message}", 500, false);
            }
        }
    }
}