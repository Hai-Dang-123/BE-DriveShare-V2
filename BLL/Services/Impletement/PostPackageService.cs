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
    public class PostPackageService : IPostPackageService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;
        private readonly IShippingRouteService _shippingRouteService;
        private readonly IPostContactService _postContactService;
        public PostPackageService(IUnitOfWork unitOfWork, UserUtility userUtility, IShippingRouteService shippingRouteService, IPostContactService postContactService)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _shippingRouteService = shippingRouteService;
            _postContactService = postContactService;
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
        //public async Task<ResponseDTO> CreateProviderPostPackageAsync(PostPackageCreateDTO postPackageCreateDTO)
        //{
        //    try
        //    {
        //        var userId = _userUtility.GetUserIdFromToken();
        //        if (userId == Guid.Empty)
        //        {
        //            return new ResponseDTO
        //            {
        //                IsSuccess = false,
        //                StatusCode = StatusCodes.Status401Unauthorized,
        //                Message = "Invalid user token.",
        //            };
        //        }
        //        var postPackage = new PostPackage
        //        {
        //            PostPackageId = Guid.NewGuid(),
        //            ProviderId = userId,
        //            Title = postPackageCreateDTO.Title,
        //            Description = postPackageCreateDTO.Description,
        //            OfferedPrice = postPackageCreateDTO.OfferedPrice,
        //            Created = DateTime.UtcNow,
        //            Updated = DateTime.UtcNow,
        //            Status = PostStatus.OPEN,
        //            ShippingRouteId = postPackageCreateDTO.ShippingRouteId,
        //        };
        //        await _unitOfWork.PostPackageRepo.AddAsync(postPackage);
        //        await _unitOfWork.SaveChangeAsync();
        //        return new ResponseDTO
        //        {
        //            IsSuccess = true,
        //            StatusCode = StatusCodes.Status201Created,
        //            Message = "Post package created successfully.",
        //            Result = postPackage,
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        return new ResponseDTO
        //        {
        //            IsSuccess = false,
        //            StatusCode = StatusCodes.Status500InternalServerError,
        //            Message = $"An error occurred: {ex.Message}",
        //        };
        //    }

        //}

        // (Thêm vào bên trong lớp PostPackageService của bạn)
        // Nhớ import: using Common.DTOs;
        // Nhớ import: using Microsoft.EntityFrameworkCore;

        // ----- CÁC HÀM MỚI ĐỂ GET VÀ PHÂN TRANG -----

        public async Task<ResponseDTO> GetAllPostPackagesAsync(
      int pageNumber,
      int pageSize,
      string? search = null,
      string? sortBy = "created",
      string? sortOrder = "desc")
        {
            try
            {
                var query = _unitOfWork.PostPackageRepo
                    .GetAllQueryable()
                    .Include(p => p.Provider)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.StartLocation)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.EndLocation)
                    .Include(p => p.Packages)
                    .AsQueryable();

            
                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.Trim().ToLower();

                    query = query.Where(p =>
                        p.Title.ToLower().Contains(search) ||
                        p.Description.ToLower().Contains(search) ||
                        p.Provider.FullName.ToLower().Contains(search) ||
                        p.ShippingRoute.StartLocation.Address.ToLower().Contains(search) ||
                        p.ShippingRoute.EndLocation.Address.ToLower().Contains(search)
                    );
                }

               
                sortBy = sortBy?.ToLower();
                sortOrder = sortOrder?.ToLower() ?? "desc";

                query = (sortBy, sortOrder) switch
                {
                    ("title", "asc") => query.OrderBy(p => p.Title),
                    ("title", "desc") => query.OrderByDescending(p => p.Title),

                    ("price", "asc") => query.OrderBy(p => p.OfferedPrice),
                    ("price", "desc") => query.OrderByDescending(p => p.OfferedPrice),

                    ("packages", "asc") => query.OrderBy(p => p.Packages.Count),
                    ("packages", "desc") => query.OrderByDescending(p => p.Packages.Count),

                    ("created", "asc") => query.OrderBy(p => p.Created),
                    _ => query.OrderByDescending(p => p.Created), // default
                };

          
                var totalCount = await query.CountAsync();

                var pagedData = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var dtos = pagedData.Select(MapToReadDTO).ToList();

                var paginatedResult = new PaginatedDTO<PostPackageReadDTO>(
                    dtos, totalCount, pageNumber, pageSize);

                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Post packages retrieved successfully.",
                    Result = paginatedResult
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = ex.Message
                };
            }
        }


        public async Task<ResponseDTO> GetPostPackagesByProviderIdAsync(Guid providerId, int pageNumber, int pageSize)
        {
            try
            {
                var query = _unitOfWork.PostPackageRepo.GetByProviderIdQueryable(providerId);
                var totalCount = await query.CountAsync();

                var pagedData = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Map Entity sang DTO
                var dtos = pagedData.Select(MapToReadDTO).ToList();

                var paginatedResult = new PaginatedDTO<PostPackageReadDTO>(dtos, totalCount, pageNumber, pageSize);

                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Post packages for provider retrieved successfully.",
                    Result = paginatedResult
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO { /* Lỗi 500 */ Message = ex.Message };
            }
        }


        // ----- HÀM MAPPER (Private) -----
        // (Thêm hàm private này vào cuối lớp PostPackageService)
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
                // Kiểm tra null để tránh lỗi
                ProviderName = p.Provider?.FullName ?? "N/A",
                ProviderAvatar = p.Provider?.AvatarUrl,
                ShippingRouteId = p.ShippingRouteId,
                // Giả định ShippingRoute có các thuộc tính này
                StartLocation = p.ShippingRoute?.StartLocation,
                EndLocation = p.ShippingRoute?.EndLocation,
                PackageCount = p.Packages?.Count ?? 0,
                ShippingRoute = new ShippingRouteInPostDTO
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


        // (Nhớ import: using Microsoft.EntityFrameworkCore;)
        // (Giả sử bạn đã có hàm private MapToReadDTO từ lần trước)

        public async Task<ResponseDTO> GetMyPostPackagesAsync(int pageNumber, int pageSize)
        {
            try
            {
                // 1. Lấy userId (ProviderId) từ token
                var providerId = _userUtility.GetUserIdFromToken();
                if (providerId == Guid.Empty)
                {
                    return new ResponseDTO
                    {
                        IsSuccess = false,
                        StatusCode = StatusCodes.Status401Unauthorized,
                        Message = "Unauthorized user."
                    };
                }

                // 2. Lấy IQueryable từ Repo (dùng lại hàm cũ)
                var query = _unitOfWork.PostPackageRepo.GetByProviderIdQueryable(providerId);

                // 3. Đếm tổng số
                var totalCount = await query.CountAsync();

                // 4. Lấy dữ liệu phân trang
                var pagedData = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // 5. Map sang DTO
                var dtos = pagedData.Select(MapToReadDTO).ToList(); // Dùng lại hàm MapToReadDTO

                // 6. Tạo kết quả phân trang
                var paginatedResult = new PaginatedDTO<PostPackageReadDTO>(dtos, totalCount, pageNumber, pageSize);

                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Your post packages retrieved successfully.",
                    Result = paginatedResult
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = $"An error occurred: {ex.Message}"
                };
            }
        }

        public async Task<ResponseDTO> GetPostPackageDetailsAsync(Guid postPackageId)
        {
            try
            {
                var postPackage = await _unitOfWork.PostPackageRepo.GetDetailsByIdAsync(postPackageId);

                if (postPackage == null)
                {
                    return new ResponseDTO
                    {
                        IsSuccess = false,
                        StatusCode = StatusCodes.Status404NotFound,
                        Message = "Post package not found."
                    };
                }

                // Map sang DTO chi tiết
                var dto = MapToDetailDTO(postPackage);

                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Post package details retrieved successfully.",
                    Result = dto
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = $"An error occurred: {ex.Message}"
                };
            }
        }

        // --- HÀM MAPPER (Private) MỚI ---
        // (Thêm hàm này vào cuối lớp Service)
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

                Provider = new ProviderInfoInPostDTO
                {
                    ProviderId = p.Provider.UserId,
                    FullName = p.Provider.FullName,
                    AvatarUrl = p.Provider.AvatarUrl,
                    PhoneNumber = p.Provider.PhoneNumber
                },

                ShippingRoute = new ShippingRouteInPostDTO
                {
                    ShippingRouteId = p.ShippingRoute.ShippingRouteId,
                    StartLocation = p.ShippingRoute.StartLocation,
                    EndLocation = p.ShippingRoute.EndLocation,
                    ExpectedPickupDate = p.ShippingRoute.ExpectedPickupDate,
                    ExpectedDeliveryDate = p.ShippingRoute.ExpectedDeliveryDate,
                    PickupTimeWindow = p.ShippingRoute.PickupTimeWindow,
                    DeliveryTimeWindow = p.ShippingRoute.DeliveryTimeWindow
                },

                PostContacts = p.PostContacts.Select(c => new PostContactReadDTO
                {
                    PostContactId = c.PostContactId,
                    Type = c.Type.ToString(),
                    FullName = c.FullName,
                    PhoneNumber = c.PhoneNumber,
                    Email = c.Email,

                }).ToList(),

                Packages = p.Packages.Select(pkg => new PackageForPostDTO
                {
                    PackageId = pkg.PackageId,
                    PackageCode = pkg.PackageCode,
                    Title = pkg.Title,
                    WeightKg = pkg.WeightKg,
                    VolumeM3 = pkg.VolumeM3,
                    Status = pkg.Status.ToString(),
                    PackageImages = pkg.PackageImages.Select(img => new PackageImageReadDTO
                    {
                        PackageImageId = img.PackageImageId,
                        PackageId = img.PackageId,
                        ImageUrl = img.PackageImageURL,
                        CreatedAt = img.CreatedAt
                    }).ToList(),

                    Item = new ItemForPackageInPostDTO // Lồng Item
                    {
                        ItemId = pkg.Item.ItemId,
                        ItemName = pkg.Item.ItemName,
                        Description = pkg.Item.Description,
                        DeclaredValue = pkg.Item.DeclaredValue,
                        Currency = pkg.Item.Currency,
                        Status = pkg.Item.Status.ToString(),
                        ImageUrls = pkg.Item.ItemImages.Select(iImg => new ItemImageReadDTO
                        {
                            ItemImageId = iImg.ItemImageId,
                            ItemId = iImg.ItemId,
                            ImageUrl = iImg.ItemImageURL
                        }).ToList()
                    }
                }).ToList()
            };
        }


        // (Trong lớp PostPackageService của bạn)
        // (Bạn cần inject: IUnitOfWork, UserUtility, IShippingRouteService, IPostContactService)

        public async Task<ResponseDTO> CreateProviderPostPackageAsync(PostPackageCreateDTO dto)
        {
            // (Controller sẽ validate DTO)

            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                {
                    return new ResponseDTO("Invalid user token.", 401, false);
                }

                // --- BƯỚC 1: TẠO SHIPPING ROUTE ---
                ShippingRoute newShippingRoute = await _shippingRouteService.CreateAndAddShippingRouteAsync(dto.ShippingRoute);

                // --- BƯỚC 2: TẠO POST PACKAGE ---
                var postPackage = new PostPackage
                {
                    PostPackageId = Guid.NewGuid(),
                    ProviderId = userId,
                    Title = dto.Title,
                    Description = dto.Description,
                    OfferedPrice = dto.OfferedPrice,
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow,
                    Status = dto.Status, // Mở bài đăng
                    ShippingRouteId = newShippingRoute.ShippingRouteId
                };
                await _unitOfWork.PostPackageRepo.AddAsync(postPackage);

                // --- BƯỚC 3: TẠO POST CONTACTS ---
                await _postContactService.CreateAndAddContactsAsync(
                    postPackage.PostPackageId,
                    dto.SenderContact,
                    dto.ReceiverContact
                );

                // --- BƯỚC 4: TÌM VÀ LIÊN KẾT PACKAGES (LOGIC MỚI) ---
                foreach (var packageId in dto.PackageIds)
                {
                    var package = await _unitOfWork.PackageRepo.GetByIdAsync(packageId);

                    // --- Validation (Rất quan trọng) ---
                    if (package == null)
                    {
                        throw new Exception($"Không tìm thấy Gói hàng (Package) với ID: {packageId}.");
                    }
                    if (package.ProviderId != userId)
                    {
                        throw new Exception($"Gói hàng '{package.PackageCode}' không thuộc về bạn.");
                    }
                    if (package.PostPackageId != null)
                    {
                        throw new Exception($"Gói hàng '{package.PackageCode}' đã thuộc một Bài đăng khác.");
                    }
                    if (package.Status != PackageStatus.PENDING)
                    {
                        throw new Exception($"Gói hàng '{package.PackageCode}' phải ở trạng thái PENDING mới có thể đăng.");
                    }
                    // (Bạn có thể thêm validation kiểm tra xem ItemId có tồn tại không nếu cần)

                    // --- Cập nhật liên kết ---
                    package.PostPackageId = postPackage.PostPackageId;
                    package.Status = PackageStatus.LOOKING_FOR_OWNER; // Cập nhật trạng thái phù hợp
                    // (Tùy chọn) Cập nhật trạng thái Package
                    // package.Status = PackageStatus.POSTED; // (Ví dụ: Đổi thành 'Đã đăng')

                    await _unitOfWork.PackageRepo.UpdateAsync(package);
                }

                // --- BƯỚC 5: LƯU TẤT CẢ TRONG 1 TRANSACTION ---
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status201Created,
                    Message = $"Tạo bài đăng thành công và đã liên kết {dto.PackageIds.Count} gói hàng.",
                    Result = new { PostPackageId = postPackage.PostPackageId }
                };
            }
            catch (Exception ex)
            {
                // Bất kỳ lỗi validation nào ở BƯỚC 4 (ví dụ: "Package not found")
                // hoặc lỗi Geocode ở BƯỚC 1 sẽ bị bắt tại đây và rollback.
                return new ResponseDTO
                {
                    IsSuccess = false,
                    // 400 (Bad Request) phù hợp hơn cho lỗi validation
                    StatusCode = StatusCodes.Status400BadRequest,
                    Message = ex.Message, // Trả về thông báo lỗi validation
                };
            }
        }

        // (Thêm hàm này vào bên trong lớp PostPackageService của bạn)

        public async Task<ResponseDTO> GetOpenPostPackagesAsync(int pageNumber, int pageSize)
        {
            try
            {
                // BƯỚC 1: Lấy IQueryable
                var query = _unitOfWork.PostPackageRepo.GetAllQueryable();

                // ***** THAY ĐỔI QUAN TRỌNG *****
                // Thêm bộ lọc (filter) để chỉ lấy trạng thái OPEN
                var openQuery = query.Where(p => p.Status == PostStatus.OPEN);
                // *******************************

                // BƯỚC 2: Đếm tổng số (dùng query đã lọc)
                var totalCount = await openQuery.CountAsync();

                // BƯỚC 3: Lấy dữ liệu của trang (dùng query đã lọc)
                var pagedData = await openQuery // <-- Dùng query đã lọc
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // BƯỚC 4: Map Entity sang DTO
                var dtos = pagedData.Select(MapToReadDTO).ToList();

                // BƯỚC 5: Tạo kết quả phân trang
                var paginatedResult = new PaginatedDTO<PostPackageReadDTO>(dtos, totalCount, pageNumber, pageSize);

                // BƯỚC 6: Trả về
                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Open post packages retrieved successfully.", // Cập nhật message
                    Result = paginatedResult
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = $"An error occurred while retrieving open post packages: {ex.Message}"
                };
            }
        }
    }
}
