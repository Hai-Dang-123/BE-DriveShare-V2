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
                var package = await _unitOfWork.PackageRepo
                    .GetAll()
                    .Include(p => p.Item)
                    .FirstOrDefaultAsync(p => p.PackageId == packageId);

                if (package == null)
                {
                    return new ResponseDTO
                    {
                        Result = false,
                        StatusCode = StatusCodes.Status404NotFound,
                        Message = "Package not found"
                    };
                }

                // 1. Update status của Package
                package.Status = PackageStatus.DELETED;

                // 2. Update status của Item => PENDING
                if (package.Item != null)
                {
                    package.Item.Status = Common.Enums.Status.ItemStatus.PENDING;
                    await _unitOfWork.ItemRepo.UpdateAsync(package.Item);
                }

                // 3. Update Package
                await _unitOfWork.PackageRepo.UpdateAsync(package);

                // 4. Save changes
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO
                {
                    Result = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Package deleted and item status updated to PENDING"
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
        // ==================================================================================
        // 1. GET ALL PACKAGES (ADMIN/PUBLIC) - ĐÃ SỬA LỖI
        // ==================================================================================
        public async Task<ResponseDTO> GetAllPackagesAsync(int pageNumber, int pageSize, string? search, string? sortField, string? sortOrder)
        {
            try
            {
                // 1. Base Query
                IQueryable<Package> query = _unitOfWork.PackageRepo.GetAllPackagesQueryable()
                    .Include(p => p.PackageImages) // QUAN TRỌNG: Nhớ Include ảnh để MapToDTO không bị null/rỗng
                    .AsNoTracking();

                // 2. Search
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var k = search.Trim().ToLower();
                    query = query.Where(p =>
                        (p.Title != null && p.Title.ToLower().Contains(k)) ||
                        (p.Description != null && p.Description.ToLower().Contains(k)) ||
                        (p.PackageCode != null && p.PackageCode.ToLower().Contains(k)) ||
                        (p.Provider != null && p.Provider.FullName.ToLower().Contains(k)) ||
                        (p.Owner != null && p.Owner.FullName.ToLower().Contains(k))
                    );
                }

                // 3. Sort
                query = ApplySorting(query, sortField, sortOrder);

                // 4. Paging & Executing Query
                var totalCount = await query.CountAsync();

                // --- SỬA Ở ĐÂY ---
                // Bước 4.1: Lấy dữ liệu thô từ DB về trước
                var rawData = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(); // Ngắt kết nối với EF Core tại đây

                // Bước 4.2: Map sang DTO trên RAM (Dùng C# thuần)
                var data = rawData.Select(p => MapToDTO(p)).ToList();

                return new ResponseDTO("Success", 200, true, new PaginatedDTO<PackageGetAllDTO>(data, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex)
            {
                return new ResponseDTO(ex.Message, 500, false);
            }
        }

        // ==================================================================================
        // 2. GET PACKAGES BY USER - ĐÃ SỬA LỖI
        // ==================================================================================
        public async Task<ResponseDTO> GetPackagesByUserIdAsync(int pageNumber, int pageSize, string? search, string? sortField, string? sortOrder)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                IQueryable<Package> query = _unitOfWork.PackageRepo.GetPackagesByUserIdQueryable(userId)
                    .Include(p => p.PackageImages) // Nhớ Include ảnh
                    .Where(p => p.Status != PackageStatus.DELETED)
                    .AsNoTracking();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var k = search.Trim().ToLower();
                    query = query.Where(p =>
                        (p.Title != null && p.Title.ToLower().Contains(k)) ||
                        (p.PackageCode != null && p.PackageCode.ToLower().Contains(k))
                    );
                }

                query = ApplySorting(query, sortField, sortOrder);

                var totalCount = await query.CountAsync();

                // --- SỬA Ở ĐÂY ---
                var rawData = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(); // Lấy dữ liệu về trước

                var data = rawData.Select(p => MapToDTO(p)).ToList(); // Map sau

                return new ResponseDTO("Success", 200, true, new PaginatedDTO<PackageGetAllDTO>(data, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex)
            {
                return new ResponseDTO(ex.Message, 500, false);
            }
        }

        // ==================================================================================
        // 3. GET PENDING PACKAGES BY USER - ĐÃ SỬA LỖI
        // ==================================================================================
        public async Task<ResponseDTO> GetMyPendingPackagesAsync(int pageNumber, int pageSize, string? search, string? sortField, string? sortOrder)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                IQueryable<Package> query = _unitOfWork.PackageRepo.GetPackagesByUserIdQueryable(userId)
                    .Include(p => p.PackageImages) // Nhớ Include ảnh
                    .Where(p => p.Status == PackageStatus.PENDING)
                    .AsNoTracking();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var k = search.Trim().ToLower();
                    query = query.Where(p =>
                        (p.Title != null && p.Title.ToLower().Contains(k)) ||
                        (p.PackageCode != null && p.PackageCode.ToLower().Contains(k))
                    );
                }

                query = ApplySorting(query, sortField, sortOrder);

                var totalCount = await query.CountAsync();

                // --- SỬA Ở ĐÂY ---
                var rawData = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(); // Lấy dữ liệu về trước

                var data = rawData.Select(p => MapToDTO(p)).ToList(); // Map sau

                return new ResponseDTO("Success", 200, true, new PaginatedDTO<PackageGetAllDTO>(data, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex)
            {
                return new ResponseDTO(ex.Message, 500, false);
            }
        }


        // ==================================================================================
        // PRIVATE HELPERS
        // ==================================================================================

        // Helper Sorting
        private IQueryable<Package> ApplySorting(IQueryable<Package> query, string? field, string? direction)
        {
            bool desc = direction?.ToUpper() == "DESC";
            return field?.ToLower() switch
            {
                "title" => desc ? query.OrderByDescending(p => p.Title) : query.OrderBy(p => p.Title),
                "code" => desc ? query.OrderByDescending(p => p.PackageCode) : query.OrderBy(p => p.PackageCode),
                "weight" => desc ? query.OrderByDescending(p => p.WeightKg) : query.OrderBy(p => p.WeightKg),
                "volume" => desc ? query.OrderByDescending(p => p.VolumeM3) : query.OrderBy(p => p.VolumeM3),
                "status" => desc ? query.OrderByDescending(p => p.Status) : query.OrderBy(p => p.Status),
                _ => query.OrderByDescending(p => p.CreatedAt) // Default: Mới nhất lên đầu
            };
        }

        // Helper Mapping DTO (Để tránh lặp code Select)
        // Lưu ý: Hàm này chỉ dùng được trong Select của LINQ to Entities nếu viết dạng Expression Tree,
        // nhưng ở đây ta viết inline trong Select nên C# Compiler sẽ tự inline code.
        // Tuy nhiên, để an toàn với EF Core, ta nên copy logic select vào từng hàm hoặc dùng AutoMapper ProjectTo.
        // Ở đây tôi viết thủ công lại trong từng hàm Select ở trên để đảm bảo EF Core dịch được SQL.
        // Hàm dưới đây chỉ để tham khảo hoặc dùng khi đã ToList().

        private PackageGetAllDTO MapToDTO(Package p)
        {
            return new PackageGetAllDTO
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
            };
        }




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
                IsSuccess = true,
                Result = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Package updated successfully",
                
            };
        }
    }
}
