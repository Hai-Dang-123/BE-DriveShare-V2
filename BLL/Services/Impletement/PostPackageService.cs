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
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class PostPackageService : IPostPackageService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;
        private readonly IShippingRouteService _shippingRouteService;
        private readonly IPostContactService _postContactService;
        private readonly IUserDocumentService _userDocumentService;

        public PostPackageService(
            IUnitOfWork unitOfWork,
            UserUtility userUtility,
            IShippingRouteService shippingRouteService,
            IPostContactService postContactService,
            IUserDocumentService userDocumentService)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _shippingRouteService = shippingRouteService;
            _postContactService = postContactService;
            _userDocumentService = userDocumentService;
        }

        // =============================================================================
        // PRIVATE HELPER: TỰ ĐỘNG CHECK VÀ UPDATE HẾT HẠN
        // =============================================================================
        private async Task CheckAndExpirePostsAsync(IEnumerable<PostPackage> posts)
        {
            bool hasChange = false;
            var today = DateTime.UtcNow.Date;

            foreach (var post in posts)
            {
                if (post.Status == PostStatus.OPEN && post.ShippingRoute != null)
                {
                    if (post.ShippingRoute.ExpectedPickupDate.Date < today)
                    {
                        post.Status = PostStatus.OUT_OF_DATE;
                        post.Updated = DateTime.UtcNow;
                        hasChange = true;
                    }
                }
            }

            if (hasChange)
            {
                await _unitOfWork.SaveChangeAsync();
            }
        }

        // =============================================================================
        // 1. CHANGE STATUS
        // =============================================================================
        public async Task<ResponseDTO> ChangePostPackageStatusAsync(ChangePostPackageStatusDTO dto)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var postPackage = await _unitOfWork.PostPackageRepo.GetByIdAsync(dto.PostPackageId);
                if (postPackage == null) return new ResponseDTO("Post package not found.", 404, false);

                postPackage.Status = dto.NewStatus;
                postPackage.Updated = DateTime.UtcNow;

                await _unitOfWork.PostPackageRepo.UpdateAsync(postPackage);
                await _unitOfWork.SaveChangeAsync();

                await transaction.CommitAsync();

                return new ResponseDTO("Change status successfully.", 200, true, postPackage);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO($"Error: {ex.Message}", 500, false);
            }
        }

        // =============================================================================
        // 2. CREATE POST
        // =============================================================================
        public async Task<ResponseDTO> CreateProviderPostPackageAsync(PostPackageCreateDTO dto)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Invalid user token.", 401, false);

                // --- CHECK GIẤY TỜ ---
                var verifyCheck = await _userDocumentService.ValidateUserDocumentsAsync(userId);
                if (!verifyCheck.IsValid) return new ResponseDTO(verifyCheck.Message, 403, false);

                // --- VALIDATE NGÀY GIỜ ---
                var route = dto.ShippingRoute;
                var today = DateTime.UtcNow.Date;

                if (route.ExpectedPickupDate.Date < today) return new ResponseDTO("Ngày lấy hàng dự kiến không thể ở trong quá khứ.", 400, false);
                if (route.ExpectedDeliveryDate.Date < route.ExpectedPickupDate.Date) return new ResponseDTO("Ngày giao hàng không thể trước ngày lấy hàng.", 400, false);
                if (route.StartTimeToPickup.HasValue && route.EndTimeToPickup.HasValue && route.StartTimeToPickup > route.EndTimeToPickup) return new ResponseDTO("Khung giờ lấy hàng không hợp lệ.", 400, false);
                if (route.StartTimeToDelivery.HasValue && route.EndTimeToDelivery.HasValue && route.StartTimeToDelivery > route.EndTimeToDelivery) return new ResponseDTO("Khung giờ giao hàng không hợp lệ.", 400, false);

                // --- CREATE ROUTE ---
                ShippingRoute newShippingRoute = await _shippingRouteService.CreateAndAddShippingRouteAsync(dto.ShippingRoute);

                // --- CREATE POST ---
                var postPackage = new PostPackage
                {
                    PostPackageId = Guid.NewGuid(),
                    ProviderId = userId,
                    Title = dto.Title,
                    Description = dto.Description,
                    OfferedPrice = dto.OfferedPrice,
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow,
                    Status = dto.Status,
                    ShippingRouteId = newShippingRoute.ShippingRouteId
                };
                await _unitOfWork.PostPackageRepo.AddAsync(postPackage);

                // --- CREATE CONTACTS ---
                await _postContactService.CreateAndAddContactsAsync(postPackage.PostPackageId, dto.SenderContact, dto.ReceiverContact);

                // --- LINK PACKAGES ---
                foreach (var packageId in dto.PackageIds)
                {
                    var package = await _unitOfWork.PackageRepo.GetByIdAsync(packageId);
                    if (package == null) throw new Exception($"Package ID {packageId} not found.");
                    if (package.ProviderId != userId) throw new Exception($"Package {package.PackageCode} is not yours.");
                    if (package.PostPackageId != null) throw new Exception($"Package {package.PackageCode} already in another post.");
                    if (package.Status != PackageStatus.PENDING) throw new Exception($"Package {package.PackageCode} must be PENDING.");

                    package.PostPackageId = postPackage.PostPackageId;
                    package.Status = PackageStatus.LOOKING_FOR_OWNER;
                    await _unitOfWork.PackageRepo.UpdateAsync(package);
                }

                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                return new ResponseDTO($"Tạo bài đăng thành công ({dto.PackageIds.Count} gói hàng).", 201, true, new { PostPackageId = postPackage.PostPackageId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO(ex.Message, 400, false);
            }
        }

        // =============================================================================
        // [FIXED] 3. GET ALL (Sửa var -> IQueryable<PostPackage>)
        // =============================================================================
        public async Task<ResponseDTO> GetAllPostPackagesAsync(int pageNumber, int pageSize, string? search = null, string? sortBy = "created", string? sortOrder = "desc")
        {
            try
            {
                // [FIX LỖI]: Khai báo rõ kiểu IQueryable<PostPackage>
                IQueryable<PostPackage> query = _unitOfWork.PostPackageRepo.GetAllQueryable()
                    .Include(p => p.Provider)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.StartLocation)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.EndLocation)
                    .Include(p => p.Packages);

                // --- Search ---
                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.Trim().ToLower();
                    query = query.Where(p =>
                        p.Title.ToLower().Contains(search) ||
                        p.Description.ToLower().Contains(search) ||
                        p.ShippingRoute.StartLocation.Address.ToLower().Contains(search) ||
                        p.ShippingRoute.EndLocation.Address.ToLower().Contains(search)
                    );
                }

                // --- Sort ---
                sortBy = sortBy?.ToLower();
                sortOrder = sortOrder?.ToLower() ?? "desc";

                // Bây giờ gán IOrderedQueryable vào IQueryable là hợp lệ
                query = (sortBy, sortOrder) switch
                {
                    ("title", "asc") => query.OrderBy(p => p.Title),
                    ("title", "desc") => query.OrderByDescending(p => p.Title),
                    ("price", "asc") => query.OrderBy(p => p.OfferedPrice),
                    ("price", "desc") => query.OrderByDescending(p => p.OfferedPrice),
                    ("created", "asc") => query.OrderBy(p => p.Created),
                    _ => query.OrderByDescending(p => p.Created),
                };

                var totalCount = await query.CountAsync();

                var pagedData = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                await CheckAndExpirePostsAsync(pagedData);

                var dtos = pagedData.Select(MapToReadDTO).ToList();

                return new ResponseDTO("Success", 200, true, new PaginatedDTO<PostPackageReadDTO>(dtos, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex)
            {
                return new ResponseDTO(ex.Message, 500, false);
            }
        }

        // =============================================================================
        // [FIXED] 4. GET BY PROVIDER
        // =============================================================================
        public async Task<ResponseDTO> GetPostPackagesByProviderIdAsync(Guid providerId, int pageNumber, int pageSize)
        {
            try
            {
                // Khai báo rõ kiểu
                IQueryable<PostPackage> query = _unitOfWork.PostPackageRepo.GetByProviderIdQueryable(providerId);

                // Đảm bảo include đủ (Nếu Repo chưa include thì thêm vào đây)
                // query = query.Include(...) 

                var totalCount = await query.CountAsync();
                var pagedData = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

                await CheckAndExpirePostsAsync(pagedData);

                var dtos = pagedData.Select(MapToReadDTO).ToList();
                return new ResponseDTO("Success", 200, true, new PaginatedDTO<PostPackageReadDTO>(dtos, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex) { return new ResponseDTO(ex.Message, 500, false); }
        }

        // =============================================================================
        // [FIXED] 5. GET OPEN POSTS
        // =============================================================================
        public async Task<ResponseDTO> GetOpenPostPackagesAsync(int pageNumber, int pageSize)
        {
            try
            {
                // [FIX LỖI]: Khai báo rõ kiểu IQueryable<PostPackage>
                IQueryable<PostPackage> query = _unitOfWork.PostPackageRepo.GetAllQueryable()
                    .Include(p => p.Provider)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.StartLocation)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.EndLocation)
                    .Include(p => p.Packages)
                    .Where(p => p.Status == PostStatus.OPEN)
                    .OrderByDescending(p => p.Created);

                var totalCount = await query.CountAsync();

                var pagedData = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

                await CheckAndExpirePostsAsync(pagedData);

                var validData = pagedData.Where(p => p.Status == PostStatus.OPEN).ToList();

                var dtos = validData.Select(MapToReadDTO).ToList();

                return new ResponseDTO("Success", 200, true, new PaginatedDTO<PostPackageReadDTO>(dtos, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex) { return new ResponseDTO(ex.Message, 500, false); }
        }

        // =============================================================================
        // [FIXED] 6. GET DETAILS
        // =============================================================================
        public async Task<ResponseDTO> GetPostPackageDetailsAsync(Guid postPackageId)
        {
            try
            {
                var postPackage = await _unitOfWork.PostPackageRepo.GetDetailsByIdAsync(postPackageId);
                if (postPackage == null) return new ResponseDTO("Not found.", 404, false);

                await CheckAndExpirePostsAsync(new List<PostPackage> { postPackage });

                var dto = MapToDetailDTO(postPackage);
                return new ResponseDTO("Success", 200, true, dto);
            }
            catch (Exception ex) { return new ResponseDTO(ex.Message, 500, false); }
        }

        // =============================================================================
        // [FIXED] 7. GET MY POSTS
        // =============================================================================
        public async Task<ResponseDTO> GetMyPostPackagesAsync(int pageNumber, int pageSize)
        {
            try
            {
                var providerId = _userUtility.GetUserIdFromToken();
                if (providerId == Guid.Empty) return new ResponseDTO("Unauthorized.", 401, false);

                // Khai báo rõ kiểu
                IQueryable<PostPackage> query = _unitOfWork.PostPackageRepo.GetByProviderIdQueryable(providerId)
                            .OrderByDescending(p => p.Created);

                var totalCount = await query.CountAsync();
                var pagedData = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

                await CheckAndExpirePostsAsync(pagedData);

                var dtos = pagedData.Select(MapToReadDTO).ToList();
                return new ResponseDTO("Success", 200, true, new PaginatedDTO<PostPackageReadDTO>(dtos, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex) { return new ResponseDTO(ex.Message, 500, false); }
        }


        // =============================================================================
        // MAPPERS
        // =============================================================================
        private PostPackageReadDTO MapToReadDTO(PostPackage p)
        {
            return new PostPackageReadDTO
            {
                PostPackageId = p.PostPackageId,
                Title = p.Title,
                Description = p.Description,
                Created = p.Created,
                Updated = p.Updated,
                OfferedPrice = p.OfferedPrice,
                Status = p.Status.ToString(),
                ProviderId = p.ProviderId,
                ProviderName = p.Provider?.FullName ?? "N/A",
                ProviderAvatar = p.Provider?.AvatarUrl,
                ShippingRouteId = p.ShippingRouteId,
                PackageCount = p.Packages?.Count ?? 0,
                StartLocation = p.ShippingRoute?.StartLocation,
                EndLocation = p.ShippingRoute?.EndLocation,
                ShippingRoute = p.ShippingRoute == null ? null : new ShippingRouteInPostDTO
                {
                    ShippingRouteId = p.ShippingRoute.ShippingRouteId,
                    StartLocation = p.ShippingRoute.StartLocation,
                    EndLocation = p.ShippingRoute.EndLocation,
                    ExpectedPickupDate = p.ShippingRoute.ExpectedPickupDate,
                    ExpectedDeliveryDate = p.ShippingRoute.ExpectedDeliveryDate,
                    PickupTimeWindow = p.ShippingRoute.PickupTimeWindow,
                    DeliveryTimeWindow = p.ShippingRoute.DeliveryTimeWindow
                }
            };
        }

        private PostPackageDetailDTO MapToDetailDTO(PostPackage p)
        {
            return new PostPackageDetailDTO
            {
                PostPackageId = p.PostPackageId,
                Title = p.Title,
                Description = p.Description,
                OfferedPrice = p.OfferedPrice,
                Status = p.Status.ToString(),
                Created = p.Created,
                Provider = p.Provider == null ? null : new ProviderInfoInPostDTO
                {
                    ProviderId = p.Provider.UserId,
                    FullName = p.Provider.FullName,
                    AvatarUrl = p.Provider.AvatarUrl,
                    PhoneNumber = p.Provider.PhoneNumber
                },
                ShippingRoute = p.ShippingRoute == null ? null : new ShippingRouteInPostDTO
                {
                    ShippingRouteId = p.ShippingRoute.ShippingRouteId,
                    StartLocation = p.ShippingRoute.StartLocation,
                    EndLocation = p.ShippingRoute.EndLocation,
                    ExpectedPickupDate = p.ShippingRoute.ExpectedPickupDate,
                    ExpectedDeliveryDate = p.ShippingRoute.ExpectedDeliveryDate,
                    PickupTimeWindow = p.ShippingRoute.PickupTimeWindow,
                    DeliveryTimeWindow = p.ShippingRoute.DeliveryTimeWindow
                },
                PostContacts = p.PostContacts?.Select(c => new PostContactReadDTO
                {
                    PostContactId = c.PostContactId,
                    Type = c.Type.ToString(),
                    FullName = c.FullName,
                    PhoneNumber = c.PhoneNumber,
                    Email = c.Email,
                }).ToList() ?? new List<PostContactReadDTO>(),
                Packages = p.Packages?.Select(pkg => new PackageForPostDTO
                {
                    PackageId = pkg.PackageId,
                    PackageCode = pkg.PackageCode,
                    Title = pkg.Title,
                    WeightKg = pkg.WeightKg,
                    VolumeM3 = pkg.VolumeM3,
                    Status = pkg.Status.ToString(),
                    PackageImages = pkg.PackageImages?.Select(img => new PackageImageReadDTO
                    {
                        PackageImageId = img.PackageImageId,
                        PackageId = img.PackageId,
                        ImageUrl = img.PackageImageURL,
                        CreatedAt = img.CreatedAt
                    }).ToList() ?? new List<PackageImageReadDTO>(),
                    Item = pkg.Item == null ? null : new ItemForPackageInPostDTO
                    {
                        ItemId = pkg.Item.ItemId,
                        ItemName = pkg.Item.ItemName,
                        Description = pkg.Item.Description,
                        DeclaredValue = pkg.Item.DeclaredValue,
                        Currency = pkg.Item.Currency,
                        Status = pkg.Item.Status.ToString(),
                        ImageUrls = pkg.Item.ItemImages?.Select(iImg => new ItemImageReadDTO
                        {
                            ItemImageId = iImg.ItemImageId,
                            ItemId = iImg.ItemId,
                            ImageUrl = iImg.ItemImageURL
                        }).ToList() ?? new List<ItemImageReadDTO>()
                    }
                }).ToList() ?? new List<PackageForPostDTO>()
            };
        }
    }
}