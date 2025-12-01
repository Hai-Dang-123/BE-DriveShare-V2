using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class PostTripService : IPostTripService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;

        public PostTripService(IUnitOfWork unitOfWork, UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
        }




        // =========================================================
        // 🔹 0. GET ALL POST TRIP (Admin / Public) — Có Search + Sort + Paging
        // =========================================================
        public async Task<ResponseDTO> GetAllPostTripsAsync(
            int pageNumber,
            int pageSize,
            string? search = null,
            string? sortField = null,
            string? sortDirection = "ASC"
        )
        {
            try
            {
                var query = _unitOfWork.PostTripRepo.GetAll()
                                   .AsNoTracking()
                                   .Where(p => p.Status != PostStatus.DELETED);

                // Include tất cả quan hệ
                query = IncludeFullPostTripData(query);

                // =========================================================
                // 1️⃣ SEARCH
                // =========================================================
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var keyword = search.Trim().ToLower();

                    query = query.Where(p =>
                        (p.Title != null && p.Title.ToLower().Contains(keyword)) ||
                        (p.Description != null && p.Description.ToLower().Contains(keyword)) ||

                        // Search Owner name
                        (p.Owner != null && p.Owner.FullName != null &&
                         p.Owner.FullName.ToLower().Contains(keyword)) ||

                        // Search Route
                        (p.Trip != null &&
                            (
                                (p.Trip.ShippingRoute.StartLocation.Address != null &&
                                 p.Trip.ShippingRoute.StartLocation.Address.ToLower().Contains(keyword)) ||
                                (p.Trip.ShippingRoute.EndLocation.Address != null &&
                                 p.Trip.ShippingRoute.EndLocation.Address.ToLower().Contains(keyword))
                            )
                        ) ||

                        // Search Vehicle
                        (p.Trip.Vehicle != null &&
                            (
                                (p.Trip.Vehicle.Model != null &&
                                 p.Trip.Vehicle.Model.ToLower().Contains(keyword)) ||
                                (p.Trip.Vehicle.PlateNumber != null &&
                                 p.Trip.Vehicle.PlateNumber.ToLower().Contains(keyword))
                            )
                        )
                    );
                }

                // =========================================================
                // 2️⃣ SORT
                // =========================================================
                bool desc = sortDirection?.ToUpper() == "DESC";

                query = sortField?.ToLower() switch
                {
                    "title" => desc ? query.OrderByDescending(p => p.Title)
                                    : query.OrderBy(p => p.Title),

                    "status" => desc ? query.OrderByDescending(p => p.Status)
                                     : query.OrderBy(p => p.Status),

                    "createdat" => desc ? query.OrderByDescending(p => p.CreateAt)
                                        : query.OrderBy(p => p.CreateAt),

                    "ownername" => desc ? query.OrderByDescending(p => p.Owner.FullName)
                                        : query.OrderBy(p => p.Owner.FullName),

                    _ => query.OrderBy(p => p.CreateAt)   // default: newest first
                };

                // =========================================================
                // 3️⃣ PAGING
                // =========================================================
                var totalCount = await query.CountAsync();

                var postTrips = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var dtoList = postTrips.Select(p => MapToPostTripViewDTO(p)).ToList();

                var paginatedResult = new PaginatedDTO<PostTripViewDTO>(
                    dtoList, totalCount, pageNumber, pageSize
                );

                return new ResponseDTO("Retrieved all post trips successfully", 200, true, paginatedResult);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting post trips: {ex.Message}", 500, false);
            }
        }

        // 1. CREATE POST TRIP
        public async Task<ResponseDTO> CreatePostTripAsync(PostTripCreateDTO dto)
        {
            try
            {
                var ownerId = _userUtility.GetUserIdFromToken();
                if (ownerId == Guid.Empty)
                    return new ResponseDTO("Unauthorized or invalid token", 401, false);

                var trip = await _unitOfWork.TripRepo.GetByIdAsync(dto.TripId);
                if (trip == null || trip.OwnerId != ownerId)
                    return new ResponseDTO("Trip not found or does not belong to this owner", 404, false);

                // =================================================================================
                // 🛑 VALIDATE: KIỂM TRA TÀI XẾ CHÍNH (PRIMARY DRIVER)
                // =================================================================================

                // 1. Kiểm tra xem bài đăng này có ý định tuyển Tài xế chính không?
                bool isRecruitingMainDriver = dto.PostTripDetails.Any(d => d.Type == Common.Enums.Type.DriverType.PRIMARY);

                if (isRecruitingMainDriver)
                {
                    // 2. Nếu có tuyển Main Driver -> Check DB xem Trip đã có Main Driver nào được chấp nhận chưa
                    bool mainDriverExists = await _unitOfWork.TripDriverAssignmentRepo.AnyAsync(
                        a => a.TripId == dto.TripId &&
                             a.Type == Common.Enums.Type.DriverType.PRIMARY &&
                             a.AssignmentStatus == Common.Enums.Status.AssignmentStatus.ACCEPTED
                    );

                    if (mainDriverExists)
                    {
                        return new ResponseDTO("Chuyến đi này đã có Tài xế chính (Primary Driver). Không thể tạo bài tuyển thêm.", 400, false);
                    }
                }
                // =================================================================================

                var postTrip = new PostTrip
                {
                    PostTripId = Guid.NewGuid(),
                    OwnerId = ownerId,
                    TripId = dto.TripId,
                    Title = dto.Title,
                    Description = dto.Description,
                    RequiredPayloadInKg = dto.RequiredPayloadInKg,
                    Status = dto.Status,
                    CreateAt = DateTime.Now,
                    UpdateAt = DateTime.Now,
                };

                foreach (var detailDto in dto.PostTripDetails)
                {
                    var detail = new PostTripDetail
                    {
                        PostTripDetailId = Guid.NewGuid(),
                        PostTripId = postTrip.PostTripId,
                        Type = detailDto.Type,
                        RequiredCount = detailDto.RequiredCount,
                        PricePerPerson = detailDto.PricePerPerson,
                        PickupLocation = detailDto.PickupLocation,
                        DropoffLocation = detailDto.DropoffLocation,
                        MustPickAtGarage = detailDto.MustPickAtGarage,
                        MustDropAtGarage = detailDto.MustDropAtGarage
                    };
                    postTrip.PostTripDetails.Add(detail);
                }

                await _unitOfWork.PostTripRepo.AddAsync(postTrip);
                await _unitOfWork.SaveChangeAsync();

                var result = new { PostTripId = postTrip.PostTripId };
                return new ResponseDTO("Create Post Trip Successfully!", 201, true, result);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error while creating Post Trip: {ex.Message}", 500, false);
            }
        }

        // 2. GET ALL (Public) - Phân trang VÀ Lọc OPEN
        public async Task<ResponseDTO> GetAllOpenPostTripsAsync(int pageNumber, int pageSize)
        {
            try
            {
                var query = _unitOfWork.PostTripRepo.GetAll()
                                         .AsNoTracking()
                                         .Where(p => p.Status == PostStatus.OPEN);

                // [SỬA ĐỔI] - Gọi hàm Include đầy đủ
                query = IncludeFullPostTripData(query);

                var totalCount = await query.CountAsync();

                var postTrips = await query
                    .OrderByDescending(p => p.CreateAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var result = postTrips.Select(p => MapToPostTripViewDTO(p)).ToList();
                var paginatedResult = new PaginatedDTO<PostTripViewDTO>(result, totalCount, pageNumber, pageSize);

                return new ResponseDTO("Get all open post trips successfully", 200, true, paginatedResult);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error while getting post trips: {ex.Message}", 500, false);
            }
        }

        // 3. GET ALL BY OWNER ID (Private) - CÓ PHÂN TRANG
        public async Task<ResponseDTO> GetMyPostTripsAsync(int pageNumber, int pageSize)
        {
            try
            {
                var ownerId = _userUtility.GetUserIdFromToken();
                if (ownerId == Guid.Empty)
                    return new ResponseDTO("Unauthorized or invalid token", 401, false);

                var query = _unitOfWork.PostTripRepo.GetAll()
                                         .AsNoTracking()
                                         .Where(p => p.OwnerId == ownerId && p.Status != PostStatus.DELETED);

                // [SỬA ĐỔI] - Gọi hàm Include đầy đủ
                query = IncludeFullPostTripData(query);

                var totalCount = await query.CountAsync();

                var postTrips = await query
                    .OrderByDescending(p => p.CreateAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var result = postTrips.Select(p => MapToPostTripViewDTO(p)).ToList();
                var paginatedResult = new PaginatedDTO<PostTripViewDTO>(result, totalCount, pageNumber, pageSize);

                return new ResponseDTO("Get my post trips successfully", 200, true, paginatedResult);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error while getting my post trips: {ex.Message}", 500, false);
            }
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

        /// <summary>
        /// Thêm các Include cần thiết để MapToPostTripViewDTO có "đủ thông tin"
        /// </summary>
        private IQueryable<PostTrip> IncludeFullPostTripData(IQueryable<PostTrip> query)
        {
            return query
                .Include(p => p.Owner) // Lấy thông tin Owner
                .Include(p => p.PostTripDetails) // Lấy các slot
                                                 // Include thông tin Trip (Lộ trình)
                .Include(p => p.Trip)
                    .ThenInclude(t => t.ShippingRoute)
                        .ThenInclude(sr => sr.StartLocation)
                .Include(p => p.Trip)
                    .ThenInclude(t => t.ShippingRoute)
                        .ThenInclude(sr => sr.EndLocation)
                .Include(p => p.Trip)
                    .ThenInclude(t => t.ShippingRoute)
                        .ThenInclude(sr => sr.PickupTimeWindow)
                // [THÊM MỚI] - Include thông tin Xe (Vehicle)
                .Include(p => p.Trip)
                    .ThenInclude(t => t.Vehicle)
                        .ThenInclude(v => v.VehicleType)
                // [THÊM MỚI] - Include thông tin Hàng hóa (Packages)
                .Include(p => p.Trip)
                    .ThenInclude(t => t.Packages);
        }


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
                    StartTime = p.Trip.ShippingRoute?.PickupTimeWindow?.StartTime ?? default(TimeOnly),

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
                    MustDropAtGarage = d.MustDropAtGarage
                }).ToList()
            };
        }

        // Trong BLL/Services/Impletement/PostTripService.cs

        public async Task<ResponseDTO> ChangePostTripStatusAsync(Guid postTripId, PostStatus newStatus)
        {
            try
            {
                // 1. Validate User (Chỉ Owner mới được đổi trạng thái bài đăng của mình)
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO("Unauthorized", 401, false);

                // 2. Lấy PostTrip
                var postTrip = await _unitOfWork.PostTripRepo.GetByIdAsync(postTripId);
                if (postTrip == null)
                    return new ResponseDTO("Post Trip not found", 404, false);

                // 3. Validate Quyền sở hữu
                if (postTrip.OwnerId != userId)
                    return new ResponseDTO("Forbidden: You do not own this post", 403, false);

                // 4. Validate Logic chuyển trạng thái (Optional but Recommended)
                // Ví dụ: Không thể chuyển từ DONE về OPEN
                if (postTrip.Status == PostStatus.DONE && newStatus == PostStatus.OPEN)
                {
                    return new ResponseDTO("Cannot reopen a completed post.", 400, false);
                }

                // Ví dụ: Không thể chuyển từ DELETED về bất kỳ trạng thái nào
                if (postTrip.Status == PostStatus.DELETED)
                {
                    return new ResponseDTO("Cannot modify a deleted post.", 400, false);
                }

                // 5. Cập nhật
                postTrip.Status = newStatus;
                postTrip.UpdateAt = DateTime.UtcNow;

                await _unitOfWork.PostTripRepo.UpdateAsync(postTrip);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO($"Status updated to {newStatus} successfully", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error changing status: {ex.Message}", 500, false);
            }
        }
    }
}