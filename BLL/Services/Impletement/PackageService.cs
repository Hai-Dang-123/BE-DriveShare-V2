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
using System.Linq.Expressions;
using System.Runtime.InteropServices;
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

        // ==================================================================================
        // 1. DELETE PACKAGE (HARD DELETE)
        // ==================================================================================
        public async Task<ResponseDTO> DeletePackageAsync(Guid packageId)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync(); // Dùng transaction cho an toàn
            try
            {
                // 1. Lấy Package kèm Item
                var package = await _unitOfWork.PackageRepo
                    .GetAll()
                    .Include(p => p.Item)
                    .FirstOrDefaultAsync(p => p.PackageId == packageId);

                if (package == null)
                {
                    return new ResponseDTO("Package not found", StatusCodes.Status404NotFound, false);
                }

                // 2. Validate: Không cho xóa nếu đang vận chuyển
                if (package.Status == PackageStatus.IN_PROGRESS)
                {
                    return new ResponseDTO("Không thể xóa gói hàng đang trong quá trình vận chuyển (IN_PROGRESS).", StatusCodes.Status400BadRequest, false);
                }

                // 3. Cập nhật trạng thái Item về PENDING (Open lại Item)
                // Bước này cực kỳ quan trọng để Item có thể được gán cho Package khác
                if (package.Item != null)
                {
                    package.Item.Status = ItemStatus.PENDING;

                    // Cập nhật Item xuống DB
                    await _unitOfWork.ItemRepo.UpdateAsync(package.Item);
                }

                // 4. Xóa vĩnh viễn Package (Hard Delete)
                // Hàm này sẽ delete row khỏi table Packages
                // Giả sử Repo của bạn có hàm Delete hoặc Remove
                await _unitOfWork.PackageRepo.DeleteAsync(package.PackageId);
                // Hoặc: _unitOfWork.PackageRepo.Remove(package); tùy implement của bạn

                // 5. Save & Commit
                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                return new ResponseDTO("Đã xóa vĩnh viễn Package. Item đã trở về trạng thái PENDING và sẵn sàng đóng gói lại.", StatusCodes.Status200OK, true);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO($"Lỗi: {ex.Message}", StatusCodes.Status500InternalServerError, false);
            }
        }
        // ==================================================================================
        // 2. GET PACKAGE BY ID (Chi tiết - Optimized)
        // ==================================================================================
        public async Task<ResponseDTO> GetPackageByIdAsync(Guid packageId)
        {
            try
            {
                // Dùng AsNoTracking để tăng tốc độ đọc
                var package = await _unitOfWork.PackageRepo.GetAll()
                    .AsNoTracking()
                    .Include(p => p.Item).ThenInclude(i => i.ItemImages)
                    .Include(p => p.PackageImages)
                    .Include(p => p.HandlingDetail)
                    .FirstOrDefaultAsync(p => p.PackageId == packageId);

                if (package == null)
                {
                    return new ResponseDTO("Package not found", StatusCodes.Status404NotFound, false);
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

                    OwnerId = package.OwnerId,
                    ProviderId = package.ProviderId,
                    ItemId = package.ItemId,
                    PostPackageId = package.PostPackageId,
                    TripId = package.TripId,

                    // Map thuộc tính bool
                    IsFragile = package.HandlingDetail?.IsFragile ?? false,
                    IsLiquid = package.HandlingDetail?.IsLiquid ?? false,
                    IsRefrigerated = package.HandlingDetail?.IsRefrigerated ?? false,
                    IsFlammable = package.HandlingDetail?.IsFlammable ?? false,
                    IsHazardous = package.HandlingDetail?.IsHazardous ?? false,
                    IsBulky = package.HandlingDetail?.IsBulky ?? false,
                    IsPerishable = package.HandlingDetail?.IsPerishable ?? false,
                    OtherRequirements = package.HandlingDetail?.OtherRequirements,

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

                return new ResponseDTO("Package retrieved successfully", StatusCodes.Status200OK, true, packageDto);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"An error occurred: {ex.Message}", StatusCodes.Status500InternalServerError, false);
            }
        }

        // ==================================================================================
        // 3. GET ALL PACKAGES (Optimized Projection)
        // ==================================================================================
        public async Task<ResponseDTO> GetAllPackagesAsync(int pageNumber, int pageSize, string? search, string? sortField, string? sortOrder)
        {
            try
            {
                // 1. Base Query (AsNoTracking để không cache vào Context -> Nhanh hơn)
                IQueryable<Package> query = _unitOfWork.PackageRepo.GetAllPackagesQueryable()
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

                // 4. Count Total
                var totalCount = await query.CountAsync();

                // 5. [OPTIMIZATION KEY] Projection (Select) trực tiếp trong SQL
                // Thay vì lấy Entity rồi Map, ta Map ngay trong câu lệnh Select
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
                        OwnerId = p.OwnerId,
                        ProviderId = p.ProviderId,
                        PostPackageId = p.PostPackageId,
                        TripId = p.TripId,
                        CreatedAt = p.CreatedAt,

                        // Map Bool trực tiếp (Cẩn thận null check trong LINQ to SQL)
                        IsFragile = p.HandlingDetail != null && p.HandlingDetail.IsFragile,
                        IsLiquid = p.HandlingDetail != null && p.HandlingDetail.IsLiquid,
                        IsRefrigerated = p.HandlingDetail != null && p.HandlingDetail.IsRefrigerated,
                        IsFlammable = p.HandlingDetail != null && p.HandlingDetail.IsFlammable,
                        IsHazardous = p.HandlingDetail != null && p.HandlingDetail.IsHazardous,
                        IsBulky = p.HandlingDetail != null && p.HandlingDetail.IsBulky,
                        IsPerishable = p.HandlingDetail != null && p.HandlingDetail.IsPerishable,
                        OtherRequirements = p.HandlingDetail != null ? p.HandlingDetail.OtherRequirements : null,

                        // Map List Ảnh
                        PackageImages = p.PackageImages.Select(img => new PackageImageReadDTO
                        {
                            PackageImageId = img.PackageImageId,
                            PackageId = img.PackageId,
                            ImageUrl = img.PackageImageURL,
                            CreatedAt = img.CreatedAt
                        }).ToList()
                    })
                    .ToListAsync(); // Thực thi query tại đây

                return new ResponseDTO("Success", 200, true, new PaginatedDTO<PackageGetAllDTO>(data, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex)
            {
                return new ResponseDTO(ex.Message, 500, false);
            }
        }

        // ==================================================================================
        // 4. GET PACKAGES BY USER (Optimized Projection)
        // ==================================================================================
        public async Task<ResponseDTO> GetPackagesByUserIdAsync(int pageNumber, int pageSize, string? search, string? sortField, string? sortOrder)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                IQueryable<Package> query = _unitOfWork.PackageRepo.GetPackagesByUserIdQueryable(userId)
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

                // Projection trực tiếp
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
                        OwnerId = p.OwnerId,
                        ProviderId = p.ProviderId,
                        PostPackageId = p.PostPackageId,
                        TripId = p.TripId,
                        CreatedAt = p.CreatedAt,

                        IsFragile = p.HandlingDetail != null && p.HandlingDetail.IsFragile,
                        IsLiquid = p.HandlingDetail != null && p.HandlingDetail.IsLiquid,
                        IsRefrigerated = p.HandlingDetail != null && p.HandlingDetail.IsRefrigerated,
                        IsFlammable = p.HandlingDetail != null && p.HandlingDetail.IsFlammable,
                        IsHazardous = p.HandlingDetail != null && p.HandlingDetail.IsHazardous,
                        IsBulky = p.HandlingDetail != null && p.HandlingDetail.IsBulky,
                        IsPerishable = p.HandlingDetail != null && p.HandlingDetail.IsPerishable,
                        OtherRequirements = p.HandlingDetail != null ? p.HandlingDetail.OtherRequirements : null,

                        PackageImages = p.PackageImages.Select(img => new PackageImageReadDTO
                        {
                            PackageImageId = img.PackageImageId,
                            PackageId = img.PackageId,
                            ImageUrl = img.PackageImageURL,
                            CreatedAt = img.CreatedAt
                        }).ToList()
                    })
                    .ToListAsync();

                return new ResponseDTO("Success", 200, true, new PaginatedDTO<PackageGetAllDTO>(data, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex)
            {
                return new ResponseDTO(ex.Message, 500, false);
            }
        }

        // ==================================================================================
        // 5. GET PENDING PACKAGES BY USER (Optimized Projection)
        // ==================================================================================
        public async Task<ResponseDTO> GetMyPendingPackagesAsync(int pageNumber, int pageSize, string? search, string? sortField, string? sortOrder)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                IQueryable<Package> query = _unitOfWork.PackageRepo.GetPackagesByUserIdQueryable(userId)
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

                // Projection trực tiếp
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
                        OwnerId = p.OwnerId,
                        ProviderId = p.ProviderId,
                        PostPackageId = p.PostPackageId,
                        TripId = p.TripId,
                        CreatedAt = p.CreatedAt,

                        IsFragile = p.HandlingDetail != null && p.HandlingDetail.IsFragile,
                        IsLiquid = p.HandlingDetail != null && p.HandlingDetail.IsLiquid,
                        IsRefrigerated = p.HandlingDetail != null && p.HandlingDetail.IsRefrigerated,
                        IsFlammable = p.HandlingDetail != null && p.HandlingDetail.IsFlammable,
                        IsHazardous = p.HandlingDetail != null && p.HandlingDetail.IsHazardous,
                        IsBulky = p.HandlingDetail != null && p.HandlingDetail.IsBulky,
                        IsPerishable = p.HandlingDetail != null && p.HandlingDetail.IsPerishable,
                        OtherRequirements = p.HandlingDetail != null ? p.HandlingDetail.OtherRequirements : null,

                        PackageImages = p.PackageImages.Select(img => new PackageImageReadDTO
                        {
                            PackageImageId = img.PackageImageId,
                            PackageId = img.PackageId,
                            ImageUrl = img.PackageImageURL,
                            CreatedAt = img.CreatedAt
                        }).ToList()
                    })
                    .ToListAsync();

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
                _ => query.OrderByDescending(p => p.CreatedAt)
            };
        }

        // ==================================================================================
        // 6. OWNER CREATE PACKAGE
        // ==================================================================================
        public async Task<ResponseDTO> OwnerCreatePackageAsync(PackageCreateDTO packageDTO)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO { IsSuccess = false, StatusCode = 401, Message = "Unauthorized" };

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

                    OwnerId = userId,
                    ItemId = packageDTO.ItemId,
                    Status = PackageStatus.PENDING,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.PackageRepo.AddAsync(package);

                // 2. Tạo PackageHandlingDetail
                var handlingDetail = new PackageHandlingDetail
                {
                    PackageId = package.PackageId,
                    IsFragile = packageDTO.IsFragile,
                    IsLiquid = packageDTO.IsLiquid,
                    IsRefrigerated = packageDTO.IsRefrigerated,
                    IsFlammable = packageDTO.IsFlammable,
                    IsHazardous = packageDTO.IsHazardous,
                    IsBulky = packageDTO.IsBulky,
                    IsPerishable = packageDTO.IsPerishable,
                    OtherRequirements = packageDTO.OtherRequirements
                };
                await _unitOfWork.PackageHandlingDetailRepo.AddAsync(handlingDetail);

                // 3. Hình ảnh
                if (packageDTO.PackageImages != null && packageDTO.PackageImages.Any())
                {
                    await _packageImageService.AddImagesToPackageAsync(package.PackageId, userId, packageDTO.PackageImages);
                }

                // 4. Update Item Status
                var item = await _unitOfWork.ItemRepo.GetByIdAsync(packageDTO.ItemId);
                if (item != null && item.Status != ItemStatus.IN_USE)
                {
                    item.Status = ItemStatus.IN_USE;
                    await _unitOfWork.ItemRepo.UpdateAsync(item);
                }

                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status201Created,
                    Message = "Package created successfully",
                    Result = new { PackageId = package.PackageId }
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO { IsSuccess = false, StatusCode = 500, Message = ex.Message };
            }
        }

        // ==================================================================================
        // 7. PROVIDER CREATE PACKAGE
        // ==================================================================================
        public async Task<ResponseDTO> ProviderCreatePackageAsync(PackageCreateDTO dto)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                var item = await _unitOfWork.ItemRepo.GetByIdAsync(dto.ItemId);
                if (item == null) return new ResponseDTO("Item not found", 404, false);

                bool hasAttributes = dto.IsFragile || dto.IsLiquid || dto.IsRefrigerated ||
                                     dto.IsFlammable || dto.IsHazardous || dto.IsBulky || dto.IsPerishable;

                var initialStatus = hasAttributes ? PackageStatus.PENDING : PackageStatus.REJECTED;

                var package = new Package
                {
                    PackageId = Guid.NewGuid(),
                    PackageCode = GeneratePackageCode(),
                    Title = dto.Title,
                    Description = dto.Description,
                    Quantity = dto.Quantity,
                    Unit = dto.Unit,
                    WeightKg = dto.WeightKg,
                    VolumeM3 = dto.VolumeM3,
                    ProviderId = userId,
                    ItemId = dto.ItemId,
                    Status = initialStatus,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.PackageRepo.AddAsync(package);

                var handlingDetail = new PackageHandlingDetail
                {
                    PackageId = package.PackageId,
                    IsFragile = dto.IsFragile,
                    IsLiquid = dto.IsLiquid,
                    IsRefrigerated = dto.IsRefrigerated,
                    IsFlammable = dto.IsFlammable,
                    IsHazardous = dto.IsHazardous,
                    IsBulky = dto.IsBulky,
                    IsPerishable = dto.IsPerishable,
                    OtherRequirements = dto.OtherRequirements
                };

                await _unitOfWork.PackageHandlingDetailRepo.AddAsync(handlingDetail);

                if (dto.PackageImages != null && dto.PackageImages.Any())
                {
                    await _packageImageService.AddImagesToPackageAsync(package.PackageId, userId, dto.PackageImages);
                }

                if (item.Status != ItemStatus.IN_USE)
                {
                    item.Status = ItemStatus.IN_USE;
                    await _unitOfWork.ItemRepo.UpdateAsync(item);
                }

                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                var msg = initialStatus == PackageStatus.PENDING
                    ? "Tạo gói hàng thành công."
                    : "Tạo gói hàng thành công (Trạng thái CHỜ do chưa chọn tính chất hàng hóa).";

                return new ResponseDTO(msg, 201, true, new { PackageId = package.PackageId, Status = initialStatus.ToString() });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO($"Lỗi: {ex.Message}", 500, false);
            }
        }

        // ==================================================================================
        // 8. UPDATE PACKAGE
        // ==================================================================================
        public async Task<ResponseDTO> UpdatePackageAsync(PackageUpdateDTO updatePackageDTO)
        {
            try
            {
                var package = await _unitOfWork.PackageRepo.GetAll()
                    .Include(p => p.HandlingDetail)
                    .FirstOrDefaultAsync(p => p.PackageId == updatePackageDTO.PackageId);

                if (package == null)
                    return new ResponseDTO { Result = false, StatusCode = StatusCodes.Status404NotFound, Message = "Package not found" };

                package.Title = updatePackageDTO.Title;
                package.Description = updatePackageDTO.Description;
                package.Quantity = updatePackageDTO.Quantity;
                package.Unit = updatePackageDTO.Unit;
                package.WeightKg = updatePackageDTO.WeightKg;
                package.VolumeM3 = updatePackageDTO.VolumeM3;
                package.UpdatedAt = DateTime.UtcNow;

                if (package.HandlingDetail == null)
                {
                    package.HandlingDetail = new PackageHandlingDetail
                    {
                        PackageId = package.PackageId,
                        IsFragile = updatePackageDTO.IsFragile,
                        IsLiquid = updatePackageDTO.IsLiquid,
                        IsRefrigerated = updatePackageDTO.IsRefrigerated,
                        IsFlammable = updatePackageDTO.IsFlammable,
                        IsHazardous = updatePackageDTO.IsHazardous,
                        IsBulky = updatePackageDTO.IsBulky,
                        IsPerishable = updatePackageDTO.IsPerishable,
                        OtherRequirements = updatePackageDTO.OtherRequirements
                    };
                    await _unitOfWork.PackageHandlingDetailRepo.AddAsync(package.HandlingDetail);
                }
                else
                {
                    package.HandlingDetail.IsFragile = updatePackageDTO.IsFragile;
                    package.HandlingDetail.IsLiquid = updatePackageDTO.IsLiquid;
                    package.HandlingDetail.IsRefrigerated = updatePackageDTO.IsRefrigerated;
                    package.HandlingDetail.IsFlammable = updatePackageDTO.IsFlammable;
                    package.HandlingDetail.IsHazardous = updatePackageDTO.IsHazardous;
                    package.HandlingDetail.IsBulky = updatePackageDTO.IsBulky;
                    package.HandlingDetail.IsPerishable = updatePackageDTO.IsPerishable;
                    package.HandlingDetail.OtherRequirements = updatePackageDTO.OtherRequirements;
                    await _unitOfWork.PackageHandlingDetailRepo.UpdateAsync(package.HandlingDetail);
                }

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
            catch (Exception ex)
            {
                return new ResponseDTO { IsSuccess = false, StatusCode = 500, Message = ex.Message };
            }
        }

        private string GeneratePackageCode()
        {
            return $"PKG-{Guid.NewGuid().ToString("N").ToUpper().Substring(0, 8)}";
        }
    }
}