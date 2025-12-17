using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class PostTripService : IPostTripService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;
        private readonly IUserDocumentService _userDocumentService;
        private readonly INotificationService _notificationService;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public PostTripService(IUnitOfWork unitOfWork, UserUtility userUtility, IUserDocumentService userDocumentService, INotificationService notificationService, IServiceScopeFactory serviceScopeFactory)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _userDocumentService = userDocumentService;
            _notificationService = notificationService;
            _serviceScopeFactory = serviceScopeFactory;
        }

        // =========================================================================
        // 1. CREATE POST TRIP (ĐĂNG BÀI TÌM TÀI XẾ)
        // =========================================================================
        public async Task<ResponseDTO> CreatePostTripAsync(PostTripCreateDTO dto)
        {
            // Dùng Transaction để đảm bảo tính toàn vẹn (PostTrip + PostTripDetails)
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var ownerId = _userUtility.GetUserIdFromToken();
                if (ownerId == Guid.Empty) return new ResponseDTO("Unauthorized or invalid token", 401, false);

                // --- BƯỚC 0: CHECK GIẤY TỜ (QUAN TRỌNG) ---
                var verifyCheck = await _userDocumentService.ValidateUserDocumentsAsync(ownerId);
                if (!verifyCheck.IsValid)
                {
                    // Trả về lỗi 403 Forbidden nếu chưa xác thực giấy tờ
                    return new ResponseDTO(verifyCheck.Message, 403, false);
                }

                // 1. Validate Trip
                var trip = await _unitOfWork.TripRepo.GetByIdAsync(dto.TripId);
                if (trip == null) return new ResponseDTO("Trip not found.", 404, false);
                if (trip.OwnerId != ownerId) return new ResponseDTO("Forbidden: Bạn không sở hữu chuyến đi này.", 403, false);

                // 2. Validate Trạng thái Trip (Không thể đăng bài cho chuyến đã xong/hủy)
                if (trip.Status == TripStatus.COMPLETED || trip.Status == TripStatus.CANCELLED || trip.Status != TripStatus.PENDING_DRIVER_ASSIGNMENT)
                {
                    return new ResponseDTO("Không thể đăng bài cho chuyến đã hoàn thành hoặc bị hủy.", 400, false);
                }

                // =================================================================================
                // 🛑 VALIDATE: KIỂM TRA TÀI XẾ CHÍNH (PRIMARY DRIVER)
                // =================================================================================

                // Kiểm tra xem trong các detail của bài đăng MỚI này có tuyển PRIMARY không?
                bool isRecruitingMainDriver = dto.PostTripDetails.Any(d => d.Type == DriverType.PRIMARY);

                if (isRecruitingMainDriver)
                {
                    // Nếu có tuyển, phải kiểm tra xem Trip đã có ai làm PRIMARY chưa (Tính cả những người đã ACCEPTED)
                    bool mainDriverExists = await _unitOfWork.TripDriverAssignmentRepo.AnyAsync(
                        a => a.TripId == dto.TripId &&
                             a.Type == DriverType.PRIMARY &&
                             a.AssignmentStatus == AssignmentStatus.ACCEPTED
                    );

                    if (mainDriverExists)
                    {
                        return new ResponseDTO("Chuyến đi này ĐÃ CÓ Tài xế chính. Bạn chỉ có thể tuyển thêm Phụ xe (Assistant).", 400, false);
                    }
                }
                // =================================================================================

                // 3. Tạo PostTrip Entity
                var postTrip = new PostTrip
                {
                    PostTripId = Guid.NewGuid(),
                    OwnerId = ownerId,
                    TripId = dto.TripId,
                    Title = dto.Title,
                    Description = dto.Description,
                    RequiredPayloadInKg = dto.RequiredPayloadInKg,
                    Status = PostStatus.OPEN, // Mặc định là OPEN khi tạo mới
                    CreateAt = DateTime.UtcNow,
                    UpdateAt = DateTime.UtcNow,
                };

                // 4. Tạo Details
                foreach (var detailDto in dto.PostTripDetails)
                {
                    var detail = new PostTripDetail
                    {
                        PostTripDetailId = Guid.NewGuid(),
                        PostTripId = postTrip.PostTripId,
                        Type = detailDto.Type, // PRIMARY hoặc ASSISTANT
                        RequiredCount = detailDto.RequiredCount,
                        PricePerPerson = detailDto.PricePerPerson,

                        // [LƯU TIỀN CỌC TỪNG VỊ TRÍ]
                        DepositAmount = detailDto.DepositAmount ?? 0,
                        BonusAmount = detailDto.BonusAmount ?? 0,

                        PickupLocation = detailDto.PickupLocation,
                        DropoffLocation = detailDto.DropoffLocation,
                        MustPickAtGarage = detailDto.MustPickAtGarage,
                        MustDropAtGarage = detailDto.MustDropAtGarage
                    };
                    postTrip.PostTripDetails.Add(detail);
                }

                // 5. Lưu xuống DB
                await _unitOfWork.PostTripRepo.AddAsync(postTrip);
                await _unitOfWork.SaveChangeAsync();

                // Commit Transaction
                await transaction.CommitAsync();

                return new ResponseDTO("Tạo bài đăng tuyển tài xế thành công!", 201, true, new { PostTripId = postTrip.PostTripId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO($"Lỗi khi tạo bài đăng: {ex.Message}", 500, false);
            }
        }


        // ==================================================================================
        // 1. GET ALL POST TRIPS (ADMIN / PUBLIC)
        // ==================================================================================
        public async Task<ResponseDTO> GetAllPostTripsAsync(int pageNumber, int pageSize, string? search, string? sortField, string? sortDirection)
        {
            try
            {
                // 1. Base Query
                var query = _unitOfWork.PostTripRepo.GetAll()
                    .AsNoTracking()
                    .Where(p => p.Status != PostStatus.DELETED);

                // 2. Include
                query = IncludeFullPostTripData(query);

                // 3. Search & Sort
                query = ApplyPostTripFilter(query, search);
                query = ApplyPostTripSort(query, sortField, sortDirection);

                // 4. Paging
                var totalCount = await query.CountAsync();
                var postTrips = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

                var dtoList = postTrips.Select(MapToPostTripViewDTO).ToList();

                return new ResponseDTO("Retrieved all post trips successfully", 200, true, new PaginatedDTO<PostTripViewDTO>(dtoList, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex) { return new ResponseDTO(ex.Message, 500, false); }
        }

        // ==================================================================================
        // 2. GET ALL OPEN POST TRIPS (PUBLIC)
        // ==================================================================================
        public async Task<ResponseDTO> GetAllOpenPostTripsAsync(int pageNumber, int pageSize, string? search, string? sortField, string? sortDirection)
        {
            try
            {
                // 1. Base Query (Chỉ lấy OPEN)
                var query = _unitOfWork.PostTripRepo.GetAll()
                    .AsNoTracking()
                    .Where(p => p.Status == PostStatus.OPEN);

                // 2. Include
                query = IncludeFullPostTripData(query);

                // 3. Search & Sort
                query = ApplyPostTripFilter(query, search);
                query = ApplyPostTripSort(query, sortField, sortDirection);

                // 4. Paging
                var totalCount = await query.CountAsync();
                var postTrips = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

                var dtoList = postTrips.Select(MapToPostTripViewDTO).ToList();
                return new ResponseDTO("Get all open post trips successfully", 200, true, new PaginatedDTO<PostTripViewDTO>(dtoList, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex) { return new ResponseDTO(ex.Message, 500, false); }
        }

        // ==================================================================================
        // 3. GET MY POST TRIPS (OWNER)
        // ==================================================================================
        public async Task<ResponseDTO> GetMyPostTripsAsync(int pageNumber, int pageSize, string? search, string? sortField, string? sortDirection)
        {
            try
            {
                var ownerId = _userUtility.GetUserIdFromToken();
                if (ownerId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                // 1. Base Query (Chỉ lấy của Owner)
                var query = _unitOfWork.PostTripRepo.GetAll()
                    .AsNoTracking()
                    .Where(p => p.OwnerId == ownerId && p.Status != PostStatus.DELETED);

                // 2. Include
                query = IncludeFullPostTripData(query);

                // 3. Search & Sort
                query = ApplyPostTripFilter(query, search);
                query = ApplyPostTripSort(query, sortField, sortDirection);

                // 4. Paging
                var totalCount = await query.CountAsync();
                var postTrips = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

                var dtoList = postTrips.Select(MapToPostTripViewDTO).ToList();
                return new ResponseDTO("Get my post trips successfully", 200, true, new PaginatedDTO<PostTripViewDTO>(dtoList, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex) { return new ResponseDTO(ex.Message, 500, false); }
        }

        // ==================================================================================
        // PRIVATE HELPERS (REUSABLE LOGIC)
        // ==================================================================================

        private IQueryable<PostTrip> IncludeFullPostTripData(IQueryable<PostTrip> query)
        {
            return query
                .Include(p => p.Owner)
                .Include(p => p.PostTripDetails)
                .Include(p => p.Trip).ThenInclude(t => t.ShippingRoute).ThenInclude(r => r.StartLocation)
                .Include(p => p.Trip).ThenInclude(t => t.ShippingRoute).ThenInclude(r => r.EndLocation)

                .Include(p => p.Trip).ThenInclude(t => t.Vehicle);
        }

        private IQueryable<PostTrip> ApplyPostTripFilter(IQueryable<PostTrip> query, string? search)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var k = search.Trim().ToLower();
                query = query.Where(p =>
                    (p.Title != null && p.Title.ToLower().Contains(k)) ||
                    (p.Description != null && p.Description.ToLower().Contains(k)) ||
                    (p.Owner != null && p.Owner.FullName != null && p.Owner.FullName.ToLower().Contains(k)) ||
                    // Search Route
                    (p.Trip != null && p.Trip.ShippingRoute != null &&
                     ((p.Trip.ShippingRoute.StartLocation.Address != null && p.Trip.ShippingRoute.StartLocation.Address.ToLower().Contains(k)) ||
                      (p.Trip.ShippingRoute.EndLocation.Address != null && p.Trip.ShippingRoute.EndLocation.Address.ToLower().Contains(k)))) ||
                    // Search Vehicle
                    (p.Trip != null && p.Trip.Vehicle != null &&
                     ((p.Trip.Vehicle.Model != null && p.Trip.Vehicle.Model.ToLower().Contains(k)) ||
                      (p.Trip.Vehicle.PlateNumber != null && p.Trip.Vehicle.PlateNumber.ToLower().Contains(k))))
                );
            }
            return query;
        }

        private IQueryable<PostTrip> ApplyPostTripSort(IQueryable<PostTrip> query, string? field, string? direction)
        {
            bool desc = direction?.ToUpper() == "DESC";
            return field?.ToLower() switch
            {
                "title" => desc ? query.OrderByDescending(p => p.Title) : query.OrderBy(p => p.Title),
                "status" => desc ? query.OrderByDescending(p => p.Status) : query.OrderBy(p => p.Status),
                "createdat" => desc ? query.OrderByDescending(p => p.CreateAt) : query.OrderBy(p => p.CreateAt),
                "ownername" => desc ? query.OrderByDescending(p => p.Owner.FullName) : query.OrderBy(p => p.Owner.FullName),
                _ => query.OrderByDescending(p => p.CreateAt) // Default
            };
        }

        // 4. GET BY ID
        public async Task<ResponseDTO> GetPostTripByIdAsync(Guid postTripId)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();

                var query = _unitOfWork.PostTripRepo.GetAll()
                                         .Where(p => p.PostTripId == postTripId);
                    

                // [SỬA ĐỔI] - Gọi hàm Include đầy đủ
                query = IncludeFullPostTripData(query);

                var postTrip = await query.FirstOrDefaultAsync();

                if (postTrip == null)
                {
                    return new ResponseDTO("Post Trip not found", 404, false);
                }

                bool isPubliclyVisible = postTrip.Status == PostStatus.OPEN;
                bool isOwner = (userId != Guid.Empty && postTrip.OwnerId == userId);

                if (!isPubliclyVisible && !isOwner)
                {
                    return new ResponseDTO("Forbidden: You do not have permission to view this post", 403, false);
                }

                var result = MapToPostTripViewDTO(postTrip);
                return new ResponseDTO("Get Post Trip successfully", 200, true, result);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error while getting post trip: {ex.Message}", 500, false);
            }
        }

        // ----- HELPER ĐỂ GỌI INCLUDE (Tái sử dụng) -----

      


        // ----- HÀM HELPER MAP DTO (ĐÃ CẬP NHẬT) -----
        private PostTripViewDTO MapToPostTripViewDTO(PostTrip p)
        {
            if (p == null) return null;

            return new PostTripViewDTO
            {
                PostTripId = p.PostTripId,
                Title = p.Title,
                Description = p.Description,
                Status = p.Status,
                CreateAt = p.CreateAt,
                UpdateAt = p.UpdateAt,
                // [XÓA BỎ] - Type không còn
                RequiredPayloadInKg = p.RequiredPayloadInKg,


                // Map Owner (nếu đã Include)
                Owner = p.Owner == null ? null : new OwnerSimpleDTO
                {
                    UserId = p.Owner.UserId,
                    FullName = p.Owner.FullName,
                    CompanyName = p.Owner.CompanyName,
                    AvatarUrl = p.Owner.AvatarUrl // (Giả định)
                },

                // [SỬA ĐỔI] - Map Trip với đầy đủ thông tin
                Trip = p.Trip == null ? null : new TripSummaryForPostDTO
                {
                    TripId = p.Trip.TripId,
                    StartLocationName = p.Trip.ShippingRoute?.StartLocation?.Address ?? string.Empty,
                    EndLocationName = p.Trip.ShippingRoute?.EndLocation?.Address ?? string.Empty,

                    StartDate = p.Trip.ShippingRoute?.ExpectedPickupDate ?? default(DateTime),
                    EndDate = p.Trip.ShippingRoute?.ExpectedDeliveryDate ?? default(DateTime),

                    // --- THÔNG TIN MỚI ĐÃ INCLUDE ---
                    VehicleModel = p.Trip.Vehicle?.Model,
                    VehiclePlate = p.Trip.Vehicle?.PlateNumber,
                    VehicleType = p.Trip.Vehicle?.VehicleType?.VehicleTypeName,
                    PackageCount = p.Trip.Packages?.Count ?? 0,
                    //TripDescription = p.Trip. // (Giả định Trip có trường Description)
                },

                // Map danh sách con (nếu đã Include)
                PostTripDetails = p.PostTripDetails.Select(d => new PostTripDetailViewDTO
                {
                    PostTripDetailId = d.PostTripDetailId,
                    Type = d.Type,
                    RequiredCount = d.RequiredCount,
                    PricePerPerson = d.PricePerPerson,
                    TotalBudget = d.TotalBudget,
                    PickupLocation = d.PickupLocation,
                    DropoffLocation = d.DropoffLocation,
                    MustPickAtGarage = d.MustPickAtGarage,
                    MustDropAtGarage = d.MustDropAtGarage,
                    DepositAmount = d.DepositAmount
                }).ToList()
            };
        }

        // Trong BLL/Services/Impletement/PostTripService.cs

        // Đảm bảo class PostTripService đã có:
        // private readonly IServiceScopeFactory _serviceScopeFactory;
        // Và được inject trong Constructor.

        // Trong PostTripService.cs

        public async Task<ResponseDTO> ChangePostTripStatusAsync(Guid postTripId, PostStatus newStatus)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 1. Validate User
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO("Unauthorized", 401, false);

                // 2. Lấy PostTrip
                var postTrip = await _unitOfWork.PostTripRepo.GetAll()
                    .Include(pt => pt.Owner)
                    .FirstOrDefaultAsync(pt => pt.PostTripId == postTripId);

                if (postTrip == null)
                    return new ResponseDTO("Post Trip not found", 404, false);

                // 3. Validate Quyền
                if (postTrip.OwnerId != userId)
                    return new ResponseDTO("Forbidden: You do not own this post", 403, false);

                // 4. Validate Logic
                if (postTrip.Status == PostStatus.DONE && newStatus == PostStatus.OPEN)
                    return new ResponseDTO("Cannot reopen a completed post.", 400, false);

                if (postTrip.Status == PostStatus.DELETED)
                    return new ResponseDTO("Cannot modify a deleted post.", 400, false);

                // 5. Cập nhật
                postTrip.Status = newStatus;
                postTrip.UpdateAt = DateTime.UtcNow;

                await _unitOfWork.PostTripRepo.UpdateAsync(postTrip);
                await _unitOfWork.SaveChangeAsync();

                // 6. Commit Transaction
                await transaction.CommitAsync();

                // =======================================================================
                // [LOGIC NOTIFICATION] CHỈ GỬI CHO DRIVER (TÀI XẾ)
                // =======================================================================
                if (newStatus == PostStatus.OPEN)
                {
                    try
                    {
                        // A. Chuẩn bị nội dung
                        string targetRole = "Driver"; // <--- CHỈ GỬI CHO TÀI XẾ

                        string title = "🚚 Kèo thơm! Có chuyến xe mới";
                        string ownerName = postTrip.Owner?.FullName ?? "Chủ xe";
                        string body = $"{ownerName} đang tìm tài xế cho lộ trình mới. Vào nhận chuyến ngay!";

                        var dataDict = new Dictionary<string, string>
                {
                    { "screen", "PostTripDetail" },
                    { "id", postTripId.ToString() }
                };
                        string jsonData = System.Text.Json.JsonSerializer.Serialize(dataDict);

                        // B. Lấy Role Driver từ DB
                        // Dùng trực tiếp _unitOfWork hiện tại (vì đã commit transaction trên rồi)
                        var roleEntity = await _unitOfWork.RoleRepo.GetByName(targetRole);

                        if (roleEntity != null)
                        {
                            // C. Lấy TẤT CẢ Driver đang hoạt động (Active)
                            // Dùng AsNoTracking để tối ưu tốc độ đọc
                            var targetUserIds = await _unitOfWork.BaseUserRepo.GetAll()
                                .AsNoTracking()
                                .Where(u => u.RoleId == roleEntity.RoleId && u.Status == UserStatus.ACTIVE)
                                .Select(u => u.UserId)
                                .ToListAsync();

                            // D. Lưu Notification vào DB (Bulk Insert)
                            if (targetUserIds.Any())
                            {
                                var notiEntities = targetUserIds.Select(targetId => new Notification
                                {
                                    NotificationId = Guid.NewGuid(),
                                    UserId = targetId,
                                    Title = title,
                                    Body = body,
                                    Data = jsonData,
                                    IsRead = false,
                                    CreatedAt = DateTime.UtcNow
                                }).ToList();

                                await _unitOfWork.NotificationRepo.AddRangeAsync(notiEntities);
                                await _unitOfWork.SaveChangeAsync();
                            }
                        }

                        // E. Gửi Push Notification (Firebase) tới Topic "Driver"
                        await _notificationService.SendToRoleAsync(targetRole, title, body, dataDict);
                    }
                    catch (Exception notiEx)
                    {
                        // Chỉ log lỗi, không throw ra ngoài để tránh báo lỗi giả cho người dùng
                        Console.WriteLine($"⚠️ Lỗi gửi thông báo PostTrip cho Driver: {notiEx.Message}");
                    }
                }
                // =======================================================================

                return new ResponseDTO($"Status updated to {newStatus} successfully", 200, true);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO($"Error changing status: {ex.Message}", 500, false);
            }
        }
    }
}