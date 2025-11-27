using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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
        private readonly IPackageImageService _packageImageService;
        public PackageService(IUnitOfWork unitOfWork, UserUtility userUtility, IPackageImageService packageImageService)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _packageImageService = packageImageService;
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
                package.Status = PackageStatus.DELETED;
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
        // Nhớ import: using Common.DTOs;
        public async Task<ResponseDTO> GetAllPackagesAsync(
     int pageNumber,
     int pageSize,
     string search = null,
     string sortField = null,
     string sortDirection = "ASC")
        {
            try
            {
                // ========= 1) BASE QUERY =========
                var query = _unitOfWork.PackageRepo.GetAllPackagesQueryable()
                           .AsNoTracking();

                // ========= 2) SEARCH =========
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var keyword = search.Trim().ToLower();

                    query = query.Where(p =>
                        (p.Title != null && p.Title.ToLower().Contains(keyword)) ||
                        (p.Description != null && p.Description.ToLower().Contains(keyword)) ||
                        (p.PackageCode != null && p.PackageCode.ToLower().Contains(keyword)) ||
                        (p.Provider != null && p.Provider.FullName.ToLower().Contains(keyword)) ||
                        (p.Owner != null && p.Owner.FullName.ToLower().Contains(keyword))
                    );
                }

                // ========= 3) SORT =========
                bool desc = sortDirection?.ToUpper() == "DESC";

                query = sortField?.ToLower() switch
                {
                    "title" => desc ? query.OrderByDescending(p => p.Title)
                                           : query.OrderBy(p => p.Title),

                    "weight" => desc ? query.OrderByDescending(p => p.WeightKg)
                                           : query.OrderBy(p => p.WeightKg),

                    "volume" => desc ? query.OrderByDescending(p => p.VolumeM3)
                                           : query.OrderBy(p => p.VolumeM3),

                    "code" => desc ? query.OrderByDescending(p => p.PackageCode)
                                           : query.OrderBy(p => p.PackageCode),

                    "status" => desc ? query.OrderByDescending(p => p.Status)
                                           : query.OrderBy(p => p.Status),


                    _ => query.OrderBy(p => p.Title) // default sort
                };

                // ========= 4) PAGINATION =========
                int totalCount = await query.CountAsync();

                var data = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new PackageGetAllDTO
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
                        PostPackageId = p.PostPackageId,
                        TripId = p.TripId,

                        PackageImages = p.PackageImages.Select(img => new PackageImageReadDTO
                        {
                            PackageImageId = img.PackageImageId,
                            PackageId = img.PackageId,
                            ImageUrl = img.PackageImageURL,
                            CreatedAt = img.CreatedAt
                        }).ToList()
                    })
                    .ToListAsync();

                var paginatedResult = new PaginatedDTO<PackageGetAllDTO>(
                    data,
                    totalCount,
                    pageNumber,
                    pageSize
                );

                // ========= 5) RETURN =========
                return new ResponseDTO(
                    "Packages retrieved successfully",
                    StatusCodes.Status200OK,
                    true,
                    paginatedResult
                );
            }
            catch (Exception ex)
            {
                return new ResponseDTO(
                    $"Error while getting packages: {ex.Message}",
                    StatusCodes.Status500InternalServerError,
                    false
                );
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
                var packageDto = new PackageGetByIdDTO
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
                        ImageUrls = package.Item.ItemImages?.Select(pi => new ItemImageReadDTO
                        {
                            ItemImageId = pi.ItemImageId,
                            ItemId = pi.ItemId,
                            ImageUrl = pi.ItemImageURL
                        }).ToList() ?? new List<ItemImageReadDTO>()
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
        // Get packages by user id
        // Nhớ import: using Common.DTOs;
        public async Task<ResponseDTO> GetPackagesByUserIdAsync(int pageNumber, int pageSize)
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

                // BƯỚC 1: LẤY IQUERYABLE TỪ REPO (QUAN TRỌNG)
                // Phương thức này KHÔNG ĐƯỢC .ToListAsync() bên trong Repo
                var packagesQuery = _unitOfWork.PackageRepo.GetPackagesByUserIdQueryable(userId);

                // BƯỚC 2: ĐẾM TỔNG SỐ MỤC (Thực thi COUNT trên DB)
                int totalCount = await packagesQuery.CountAsync();

                // BƯỚC 3: LẤY DỮ LIỆU CỦA TRANG HIỆN TẠI
                // Áp dụng .Skip().Take() trước, .Select() sau, .ToListAsync() cuối cùng
                // để EF tối ưu hóa truy vấn SQL
                var packageDto = await packagesQuery
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new PackageGetAllDTO
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
                        PostPackageId = p.PostPackageId,
                        TripId = p.TripId,
                        
                        PackageImages = p.PackageImages.Select(pi => new PackageImageReadDTO
                        {
                            PackageImageId = pi.PackageImageId,
                            PackageId = pi.PackageId,
                            ImageUrl = pi.PackageImageURL,
                            CreatedAt = pi.CreatedAt,
                        }).ToList() ?? new List<PackageImageReadDTO>()
                    })
                    .ToListAsync(); // Thực thi truy vấn LẤY DỮ LIỆU trên DB

                // BƯỚC 4: TẠO KẾT QUẢ PHÂN TRANG
                var paginatedResult = new PaginatedDTO<PackageGetAllDTO>(packageDto, totalCount, pageNumber, pageSize);

                // BƯỚC 5: TRẢ VỀ RESPONSE
                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Packages retrieved successfully",
                    Result = paginatedResult
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

        // (Hàm này được thêm vào lớp PackageService của bạn)

        public async Task<ResponseDTO> GetMyPendingPackagesAsync(int pageNumber, int pageSize)
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

                // BƯỚC 1: LẤY IQUERYABLE TỪ REPO
                var packagesQuery = _unitOfWork.PackageRepo.GetPackagesByUserIdQueryable(userId);

                // ***** THAY ĐỔI QUAN TRỌNG *****
                // Thêm bộ lọc (filter) để chỉ lấy trạng thái PENDING
                var pendingPackagesQuery = packagesQuery.Where(p => p.Status == PackageStatus.PENDING);
                // *******************************

                // BƯỚC 2: ĐẾM TỔNG SỐ (dùng query đã lọc)
                int totalCount = await pendingPackagesQuery.CountAsync();

                // BƯỚC 3: LẤY DỮ LIỆU CỦA TRANG HIỆN TẠI (dùng query đã lọc)
                var packageDto = await pendingPackagesQuery // <-- Dùng query đã lọc
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new PackageGetAllDTO
                    {
                        PackageId = p.PackageId,
                        PackageCode = p.PackageCode,
                        Title = p.Title,
                        Description = p.Description,
                        Quantity = p.Quantity,
                        Unit = p.Unit,
                        WeightKg = p.WeightKg,
                        VolumeM3 = p.VolumeM3,
                        Status = p.Status, // Sẽ luôn là "PENDING"
                        HandlingAttributes = p.HandlingAttributes ?? new List<string>(),
                        OtherRequirements = p.OtherRequirements,
                        OwnerId = p.OwnerId,
                        ProviderId = p.ProviderId,
                        PostPackageId = p.PostPackageId,
                        TripId = p.TripId,
                        PackageImages = p.PackageImages.Select(pi => new PackageImageReadDTO
                        {
                            PackageImageId = pi.PackageImageId,
                            PackageId = pi.PackageId,
                            ImageUrl = pi.PackageImageURL,
                            CreatedAt = pi.CreatedAt,
                        }).ToList() ?? new List<PackageImageReadDTO>()
                    })
                    .ToListAsync();

                // BƯỚC 4: TẠO KẾT QUẢ PHÂN TRANG
                var paginatedResult = new PaginatedDTO<PackageGetAllDTO>(packageDto, totalCount, pageNumber, pageSize);

                // BƯỚC 5: TRẢ VỀ RESPONSE
                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Pending packages retrieved successfully", // Cập nhật message
                    Result = paginatedResult
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
        //public async Task<ResponseDTO> OwnerCreatePackageAsync(PackageCreateDTO packageDTO)
        //{
        //    try
        //    {
        //        var userId = _userUtility.GetUserIdFromToken();
        //        if (userId == Guid.Empty)
        //            return new ResponseDTO
        //            {
        //                Result = false,
        //                StatusCode = StatusCodes.Status401Unauthorized,
        //                Message = "Unauthorized"
        //            };

        //        var package = new Package
        //        {
        //            PackageId = Guid.NewGuid(),
        //            PackageCode = packageDTO.PackageCode,
        //            Title = packageDTO.Title,
        //            Description = packageDTO.Description,
        //            Quantity = packageDTO.Quantity,
        //            Unit = packageDTO.Unit,
        //            WeightKg = packageDTO.WeightKg,
        //            VolumeM3 = packageDTO.VolumeM3,
        //            HandlingAttributes = packageDTO.HandlingAttributes ?? new(),
        //            OtherRequirements = packageDTO.OtherRequirements,
        //            OwnerId = userId,
        //            ItemId = packageDTO.ItemId,
        //            PostPackageId = packageDTO.PostPackageId,              
        //            Status = PackageStatus.PENDING,
        //            TripId = packageDTO.TripId,
        //        };

        //        await _unitOfWork.PackageRepo.AddAsync(package);
        //        await _unitOfWork.SaveChangeAsync();

        //        return new ResponseDTO
        //        {
        //            Result = true,
        //            StatusCode = StatusCodes.Status201Created,
        //            Message = "Package created successfully",

        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        return new ResponseDTO
        //        {
        //            Result = false,
        //            StatusCode = StatusCodes.Status500InternalServerError,
        //            Message = $"An error occurred: {ex.Message}"
        //        };
        //    }
        //    }
        //// Provider create package
        //public async Task<ResponseDTO> ProviderCreatePackageAsync(PackageCreateDTO packageDTO)
        //{
        //    try
        //    {
        //        var userId = _userUtility.GetUserIdFromToken();
        //        if (userId == Guid.Empty)
        //            return new ResponseDTO
        //            {
        //                Result = false,
        //                StatusCode = StatusCodes.Status401Unauthorized,
        //                Message = "Unauthorized"
        //            };

        //        var package = new Package
        //        {
        //            PackageId = Guid.NewGuid(),
        //            PackageCode = packageDTO.PackageCode,
        //            Title = packageDTO.Title,
        //            Description = packageDTO.Description,
        //            Quantity = packageDTO.Quantity,
        //            Unit = packageDTO.Unit,
        //            WeightKg = packageDTO.WeightKg,
        //            VolumeM3 = packageDTO.VolumeM3,
        //            HandlingAttributes = packageDTO.HandlingAttributes ?? new(),
        //            OtherRequirements = packageDTO.OtherRequirements,
        //            ProviderId = userId,
        //            ItemId = packageDTO.ItemId,
        //            PostPackageId = packageDTO.PostPackageId,
        //            TripId = packageDTO.TripId,
        //            Status = PackageStatus.PENDING,
        //        };

        //        await _unitOfWork.PackageRepo.AddAsync(package);
        //        await _unitOfWork.SaveChangeAsync();

        //        return new ResponseDTO
        //        {
        //            Result = true,
        //            StatusCode = StatusCodes.Status201Created,
        //            Message = "Package created successfully",

        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        return new ResponseDTO
        //        {
        //            Result = false,
        //            StatusCode = StatusCodes.Status500InternalServerError,
        //            Message = $"An error occurred: {ex.Message}"
        //        };
        //    }
        //}


        public async Task<ResponseDTO> OwnerCreatePackageAsync(PackageCreateDTO packageDTO)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO { /* Unauthorized */ };

                // 1. Tạo Package
                var package = new Package
                {
                    PackageId = Guid.NewGuid(),
                    PackageCode = GeneratePackageCode(),
                    Title = packageDTO.Title,
                    Description = packageDTO.Description,
                    Quantity = packageDTO.Quantity,
                    Unit = packageDTO.Unit,
                    WeightKg = packageDTO.WeightKg,
                    VolumeM3 = packageDTO.VolumeM3,
                    HandlingAttributes = packageDTO.HandlingAttributes ?? new(),
                    OtherRequirements = packageDTO.OtherRequirements,
                    OwnerId = userId, // Gán OwnerId
                    ItemId = packageDTO.ItemId,
                    Status = PackageStatus.PENDING,
                };

                // 2. Thêm Package vào UoW
                await _unitOfWork.PackageRepo.AddAsync(package);

                // 3. Thêm hình ảnh (nếu có) vào UoW
                if (packageDTO.PackageImages != null && packageDTO.PackageImages.Any())
                {
                    await _packageImageService.AddImagesToPackageAsync(package.PackageId, userId, packageDTO.PackageImages);
                }

                // 4. Save 1 LẦN DUY NHẤT (cho cả Package và PackageImages)
                await _unitOfWork.SaveChangeAsync(); // (Hoặc SaveAsync() tùy theo UoW của bạn)

                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status201Created,
                    Message = "Package created successfully",
                    Result = new { PackageId = package.PackageId } // Trả về ID
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO { /* 500 Error */ Message = ex.Message };
            }
        }

        // --- HÀM PROVIDER CREATE (ĐÃ CẬP NHẬT) ---
        public async Task<ResponseDTO> ProviderCreatePackageAsync(PackageCreateDTO packageDTO)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO { /* Unauthorized */ };

                // 1. Tạo Package
                var package = new Package
                {
                    PackageId = Guid.NewGuid(),
                    // (Tất cả thuộc tính DTO...)
                    PackageCode = GeneratePackageCode(),
                    Title = packageDTO.Title,
                    Description = packageDTO.Description,
                    Quantity = packageDTO.Quantity,
                    Unit = packageDTO.Unit,
                    WeightKg = packageDTO.WeightKg,
                    VolumeM3 = packageDTO.VolumeM3,
                    HandlingAttributes = packageDTO.HandlingAttributes ?? new(),
                    OtherRequirements = packageDTO.OtherRequirements,
                    ProviderId = userId, // Gán ProviderId
                    ItemId = packageDTO.ItemId,
                    Status = PackageStatus.PENDING,
                };

                // 2. Thêm Package vào UoW
                await _unitOfWork.PackageRepo.AddAsync(package);

                // 3. Thêm hình ảnh (nếu có) vào UoW
                if (packageDTO.PackageImages != null && packageDTO.PackageImages.Any())
                {
                    await _packageImageService.AddImagesToPackageAsync(package.PackageId, userId, packageDTO.PackageImages);
                }

                var item = await _unitOfWork.ItemRepo.GetByIdAsync(packageDTO.ItemId);
                if (item != null)
                {
                    item.Status = Common.Enums.Status.ItemStatus.IN_USE;
                    await _unitOfWork.ItemRepo.UpdateAsync(item);
                }

                // 4. Save 1 LẦN DUY NHẤT
                await _unitOfWork.SaveChangeAsync(); // (Hoặc SaveAsync())

                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status201Created,
                    Message = "Package created successfully",
                    Result = new { PackageId = package.PackageId } // Trả về ID
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO { /* 500 Error */ Message = ex.Message };
            }
        }

        private string GeneratePackageCode()
        {
            // Tạo một code ngẫu nhiên, dễ đọc, và duy nhất
            // Ví dụ: PKG-B7E1A0C9
            return $"PKG-{Guid.NewGuid().ToString("N").ToUpper().Substring(0, 8)}";
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
