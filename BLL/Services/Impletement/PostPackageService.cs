using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Helpers;
using Common.ValueObjects;
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
        private readonly IVietMapService _vietMapService;
        private readonly IOwnerDriverLinkService _ownerDriverLinkService;

        public PostPackageService(
            IUnitOfWork unitOfWork,
            UserUtility userUtility,
            IShippingRouteService shippingRouteService,
            IPostContactService postContactService,
            IUserDocumentService userDocumentService,
            IVietMapService vietMapService,
            IOwnerDriverLinkService ownerDriverLinkService)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _shippingRouteService = shippingRouteService;
            _postContactService = postContactService;
            _userDocumentService = userDocumentService;
            _vietMapService = vietMapService;
            _ownerDriverLinkService = ownerDriverLinkService;
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
        //public async Task<ResponseDTO> CreateProviderPostPackageAsync(PostPackageCreateDTO dto)
        //{
        //    using var transaction = await _unitOfWork.BeginTransactionAsync();
        //    try
        //    {
        //        var userId = _userUtility.GetUserIdFromToken();
        //        if (userId == Guid.Empty) return new ResponseDTO("Invalid user token.", 401, false);

        //        // --- CHECK GIẤY TỜ ---
        //        var verifyCheck = await _userDocumentService.ValidateUserDocumentsAsync(userId);
        //        if (!verifyCheck.IsValid) return new ResponseDTO(verifyCheck.Message, 403, false);

        //        // --- VALIDATE NGÀY GIỜ ---
        //        var route = dto.ShippingRoute;
        //        var today = DateTime.UtcNow.Date;

        //        if (route.ExpectedPickupDate.Date < today) return new ResponseDTO("Ngày lấy hàng dự kiến không thể ở trong quá khứ.", 400, false);
        //        if (route.ExpectedDeliveryDate.Date < route.ExpectedPickupDate.Date) return new ResponseDTO("Ngày giao hàng không thể trước ngày lấy hàng.", 400, false);
        //        if (route.StartTimeToPickup.HasValue && route.EndTimeToPickup.HasValue && route.StartTimeToPickup > route.EndTimeToPickup) return new ResponseDTO("Khung giờ lấy hàng không hợp lệ.", 400, false);
        //        if (route.StartTimeToDelivery.HasValue && route.EndTimeToDelivery.HasValue && route.StartTimeToDelivery > route.EndTimeToDelivery) return new ResponseDTO("Khung giờ giao hàng không hợp lệ.", 400, false);

        //        // --- CREATE ROUTE ---
        //        ShippingRoute newShippingRoute = await _shippingRouteService.CreateAndAddShippingRouteAsync(dto.ShippingRoute);

        //        // --- CREATE POST ---
        //        var postPackage = new PostPackage
        //        {
        //            PostPackageId = Guid.NewGuid(),
        //            ProviderId = userId,
        //            Title = dto.Title,
        //            Description = dto.Description,
        //            OfferedPrice = dto.OfferedPrice,
        //            Created = DateTime.UtcNow,
        //            Updated = DateTime.UtcNow,
        //            Status = dto.Status,
        //            ShippingRouteId = newShippingRoute.ShippingRouteId
        //        };
        //        await _unitOfWork.PostPackageRepo.AddAsync(postPackage);

        //        // --- CREATE CONTACTS ---
        //        await _postContactService.CreateAndAddContactsAsync(postPackage.PostPackageId, dto.SenderContact, dto.ReceiverContact);

        //        // --- LINK PACKAGES ---
        //        foreach (var packageId in dto.PackageIds)
        //        {
        //            var package = await _unitOfWork.PackageRepo.GetByIdAsync(packageId);
        //            if (package == null) throw new Exception($"Package ID {packageId} not found.");
        //            if (package.ProviderId != userId) throw new Exception($"Package {package.PackageCode} is not yours.");
        //            if (package.PostPackageId != null) throw new Exception($"Package {package.PackageCode} already in another post.");
        //            if (package.Status != PackageStatus.PENDING) throw new Exception($"Package {package.PackageCode} must be PENDING.");

        //            package.PostPackageId = postPackage.PostPackageId;
        //            package.Status = PackageStatus.LOOKING_FOR_OWNER;
        //            await _unitOfWork.PackageRepo.UpdateAsync(package);
        //        }

        //        await _unitOfWork.SaveChangeAsync();
        //        await transaction.CommitAsync();

        //        return new ResponseDTO($"Tạo bài đăng thành công ({dto.PackageIds.Count} gói hàng).", 201, true, new { PostPackageId = postPackage.PostPackageId });
        //    }
        //    catch (Exception ex)
        //    {
        //        await transaction.RollbackAsync();
        //        return new ResponseDTO(ex.Message, 400, false);
        //    }
        //}

        // =============================================================================
        // 3. GET ALL POST PACKAGES (ADMIN/PUBLIC)
        // =============================================================================

        // =============================================================================
        // 2. CREATE POST (ĐÃ CẬP NHẬT VALIDATE THỜI GIAN)
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

                // --- VALIDATE NGÀY GIỜ CƠ BẢN ---
                var route = dto.ShippingRoute;
                var today = DateTime.UtcNow.Date;

                if (route.ExpectedPickupDate.Date < today) return new ResponseDTO("Ngày lấy hàng dự kiến không thể ở trong quá khứ.", 400, false);
                if (route.ExpectedDeliveryDate <= route.ExpectedPickupDate) return new ResponseDTO("Ngày giao hàng phải sau thời gian lấy hàng.", 400, false);
                if (route.StartTimeToPickup.HasValue && route.EndTimeToPickup.HasValue && route.StartTimeToPickup > route.EndTimeToPickup) return new ResponseDTO("Khung giờ lấy hàng không hợp lệ.", 400, false);
                if (route.StartTimeToDelivery.HasValue && route.EndTimeToDelivery.HasValue && route.StartTimeToDelivery > route.EndTimeToDelivery) return new ResponseDTO("Khung giờ giao hàng không hợp lệ.", 400, false);

                // =======================================================================
                // [FIXED] LOGIC KIỂM TRA KHẢ THI & GEOCODE
                // =======================================================================

                // --- TÍNH TOÁN & VALIDATE LỘ TRÌNH ---
                double savedDistance = 0;
                double savedDuration = 0;

                // 1. Geocode (Giữ nguyên logic sửa lỗi Location)
                if (IsLocationMissingCoordinates(dto.ShippingRoute.StartLocation))
                {
                    var geo = await _vietMapService.GeocodeAsync(dto.ShippingRoute.StartLocation.Address);
                    if (geo != null) dto.ShippingRoute.StartLocation = new Location(dto.ShippingRoute.StartLocation.Address, geo.Latitude ?? 0, geo.Longitude ?? 0);
                }
                if (IsLocationMissingCoordinates(dto.ShippingRoute.EndLocation))
                {
                    var geo = await _vietMapService.GeocodeAsync(dto.ShippingRoute.EndLocation.Address);
                    if (geo != null) dto.ShippingRoute.EndLocation = new Location(dto.ShippingRoute.EndLocation.Address, geo.Latitude ?? 0, geo.Longitude ?? 0);
                }

                // 2. Gọi Vietmap & Validate
                if (!IsLocationMissingCoordinates(dto.ShippingRoute.StartLocation) && !IsLocationMissingCoordinates(dto.ShippingRoute.EndLocation))
                {
                    var path = await _vietMapService.GetRouteAsync(dto.ShippingRoute.StartLocation, dto.ShippingRoute.EndLocation, "truck");

                    if (path != null)
                    {
                        double rawHours = path.Time / (1000.0 * 60 * 60);
                        savedDuration = Math.Round((rawHours * 1.15) + 0.5, 1); // Buffer 15% + 30p
                        savedDistance = Math.Round(path.Distance / 1000.0, 2);

                        // Validate thời gian Provider
                        if (savedDuration > 0)
                        {
                            double providerHours = (dto.ShippingRoute.ExpectedDeliveryDate - dto.ShippingRoute.ExpectedPickupDate).TotalHours;
                            if (providerHours < savedDuration - 0.1)
                            {
                                await transaction.RollbackAsync();
                                return new ResponseDTO($"Thời gian giao hàng không khả thi. Cần tối thiểu {savedDuration} giờ.", 400, false);
                            }
                        }
                    }
                }

                // --- GÁN GIÁ TRỊ VÀO ENTITY ---
                dto.ShippingRoute.EstimatedDistanceKm = savedDistance;
                dto.ShippingRoute.EstimatedDurationHours = savedDuration;



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

        // Helper check tọa độ
        private bool IsLocationMissingCoordinates(Location loc)
        {
            // Kiểm tra null an toàn và giá trị 0
            return loc == null || !loc.Latitude.HasValue || !loc.Longitude.HasValue ||
                   (loc.Latitude.Value == 0 && loc.Longitude.Value == 0);
        }
        public async Task<ResponseDTO> GetAllPostPackagesAsync(int pageNumber, int pageSize, string? search, string? sortBy, string? sortOrder)
        {
            try
            {
                IQueryable<PostPackage> query = _unitOfWork.PostPackageRepo.GetAllQueryable()
                    .Include(p => p.Provider)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.StartLocation)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.EndLocation)
                    .Include(p => p.Packages);

                // Search & Sort
                query = ApplyPostPackageFilter(query, search);
                query = ApplyPostPackageSort(query, sortBy, sortOrder);

                // Paging
                var totalCount = await query.CountAsync();
                var pagedData = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

                await CheckAndExpirePostsAsync(pagedData);

                var dtos = pagedData.Select(MapToReadDTO).ToList();
                return new ResponseDTO("Success", 200, true, new PaginatedDTO<PostPackageReadDTO>(dtos, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex) { return new ResponseDTO(ex.Message, 500, false); }
        }

        // =============================================================================
        // 4. GET BY PROVIDER
        // =============================================================================
        public async Task<ResponseDTO> GetPostPackagesByProviderIdAsync(Guid providerId, int pageNumber, int pageSize, string? search, string? sortBy, string? sortOrder)
        {
            try
            {
                // Lưu ý: Repo nên trả về IQueryable chưa query DB
                IQueryable<PostPackage> query = _unitOfWork.PostPackageRepo.GetByProviderIdQueryable(providerId);

                // Include nếu Repo chưa include (cần check lại Repo của bạn)
                // query = query.Include(...); 

                query = ApplyPostPackageFilter(query, search);
                query = ApplyPostPackageSort(query, sortBy, sortOrder);

                var totalCount = await query.CountAsync();
                var pagedData = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

                await CheckAndExpirePostsAsync(pagedData);

                var dtos = pagedData.Select(MapToReadDTO).ToList();
                return new ResponseDTO("Success", 200, true, new PaginatedDTO<PostPackageReadDTO>(dtos, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex) { return new ResponseDTO(ex.Message, 500, false); }
        }

        // =============================================================================
        // 5. GET OPEN POSTS
        // =============================================================================
        public async Task<ResponseDTO> GetOpenPostPackagesAsync(int pageNumber, int pageSize, string? search, string? sortBy, string? sortOrder)
        {
            try
            {
                IQueryable<PostPackage> query = _unitOfWork.PostPackageRepo.GetAllQueryable()
                    .Include(p => p.Provider)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.StartLocation)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.EndLocation)
                    .Include(p => p.Packages)
                    .Where(p => p.Status == PostStatus.OPEN); // Chỉ lấy OPEN

                query = ApplyPostPackageFilter(query, search);
                query = ApplyPostPackageSort(query, sortBy, sortOrder);

                var totalCount = await query.CountAsync();
                var pagedData = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

                await CheckAndExpirePostsAsync(pagedData);

                // Lọc lại những cái vẫn còn OPEN sau khi check expire
                var validData = pagedData.Where(p => p.Status == PostStatus.OPEN).ToList();

                var dtos = validData.Select(MapToReadDTO).ToList();
                return new ResponseDTO("Success", 200, true, new PaginatedDTO<PostPackageReadDTO>(dtos, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex) { return new ResponseDTO(ex.Message, 500, false); }
        }

        // =============================================================================
        // 7. GET MY POSTS
        // =============================================================================
        public async Task<ResponseDTO> GetMyPostPackagesAsync(int pageNumber, int pageSize, string? search, string? sortBy, string? sortOrder)
        {
            try
            {
                var providerId = _userUtility.GetUserIdFromToken();
                if (providerId == Guid.Empty) return new ResponseDTO("Unauthorized.", 401, false);

                IQueryable<PostPackage> query = _unitOfWork.PostPackageRepo.GetByProviderIdQueryable(providerId);

                query = ApplyPostPackageFilter(query, search);
                query = ApplyPostPackageSort(query, sortBy, sortOrder);

                var totalCount = await query.CountAsync();
                var pagedData = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

                await CheckAndExpirePostsAsync(pagedData);

                var dtos = pagedData.Select(MapToReadDTO).ToList();
                return new ResponseDTO("Success", 200, true, new PaginatedDTO<PostPackageReadDTO>(dtos, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex) { return new ResponseDTO(ex.Message, 500, false); }
        }

        // =============================================================================
        // PRIVATE HELPERS (SEARCH & SORT)
        // =============================================================================

        private IQueryable<PostPackage> ApplyPostPackageFilter(IQueryable<PostPackage> query, string? search)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var k = search.Trim().ToLower();
                return query.Where(p =>
                    (p.Title != null && p.Title.ToLower().Contains(k)) ||
                    (p.Description != null && p.Description.ToLower().Contains(k)) ||
                    (p.ShippingRoute.StartLocation.Address.ToLower().Contains(k)) ||
                    (p.ShippingRoute.EndLocation.Address.ToLower().Contains(k))
                );
            }
            return query;
        }

        private IQueryable<PostPackage> ApplyPostPackageSort(IQueryable<PostPackage> query, string? sortBy, string? sortOrder)
        {
            bool desc = sortOrder?.ToUpper() == "DESC";
            sortBy = sortBy?.ToLower();

            return sortBy switch
            {
                "title" => desc ? query.OrderByDescending(p => p.Title) : query.OrderBy(p => p.Title),
                "price" => desc ? query.OrderByDescending(p => p.OfferedPrice) : query.OrderBy(p => p.OfferedPrice),
                "created" => desc ? query.OrderByDescending(p => p.Created) : query.OrderBy(p => p.Created),
                _ => query.OrderByDescending(p => p.Created) // Default
            };
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

                // =================================================================
                // 1. LOGIC GỢI Ý TÀI XẾ (DRIVER SUGGESTION)
                // =================================================================
                if (postPackage.ShippingRoute != null)
                {
                    double dist = postPackage.ShippingRoute.EstimatedDistanceKm;
                    double dur = postPackage.ShippingRoute.EstimatedDurationHours;

                    // FALLBACK: Nếu DB chưa có (do bài đăng cũ), gọi API tính lại
                    if (dist == 0 || dur == 0)
                    {
                        var start = postPackage.ShippingRoute.StartLocation;
                        var end = postPackage.ShippingRoute.EndLocation;

                        // Helper IsLocationMissingCoordinates đã có trong class
                        if (!IsLocationMissingCoordinates(start) && !IsLocationMissingCoordinates(end))
                        {
                            var path = await _vietMapService.GetRouteAsync(start, end, "truck");
                            if (path != null)
                            {
                                dur = Math.Round((path.Time / (1000.0 * 60 * 60) * 1.15) + 0.5, 1);
                                dist = Math.Round(path.Distance / 1000.0, 2);

                                // (Optional) Update lại vào DB để lần sau ko phải tính
                                // postPackage.ShippingRoute.EstimatedDistanceKm = dist; ...
                            }
                        }
                    }

                    // TÍNH TOÁN 3 KỊCH BẢN (NẾU CÓ DATA)
                    if (dur > 0)
                    {
                        var pickup = postPackage.ShippingRoute.ExpectedPickupDate;
                        var deadline = postPackage.ShippingRoute.ExpectedDeliveryDate;

                        // Gọi hàm helper tính toán (Code helper ở dưới)
                        dto.DriverSuggestion = TripCalculationHelper.CalculateScenarios(
                            dist, dur,
                            postPackage.ShippingRoute.ExpectedPickupDate,
                            postPackage.ShippingRoute.ExpectedDeliveryDate
                );
                    }
                }

                // =================================================================
                // 2. CHECK TÀI XẾ NỘI BỘ (INTERNAL DRIVER CHECK)
                // =================================================================
                var currentUserId = _userUtility.GetUserIdFromToken();

                // Kiểm tra user hiện tại có phải là Owner không
                // Lưu ý: Nếu Repo ExistsAsync trả về bool thì OK.
                // Kiểm tra bằng cách lấy thử, nếu khác null nghĩa là tồn tại
                var ownerObj = await _unitOfWork.OwnerRepo.GetByIdAsync(currentUserId);
                var isOwner = (ownerObj != null);

                if (isOwner && postPackage.ShippingRoute != null)
                {
                    // A. Lấy danh sách tài xế kèm thống kê giờ chạy từ Service kia
                    // Hàm GetDriversWithStatsByOwnerIdAsync phải trả về List<LinkedDriverDTO>
                    var myDrivers = await _ownerDriverLinkService.GetDriversWithStatsByOwnerIdAsync(currentUserId);

                    // =================================================================
                    // B. Check thêm lịch bận của chuyến đi này (Collision Check)
                    // =================================================================
                    var start = postPackage.ShippingRoute.ExpectedPickupDate;
                    var end = postPackage.ShippingRoute.ExpectedDeliveryDate;

                    // Lấy danh sách driverId để query lịch bận
                    var driverIds = myDrivers.Select(d => d.DriverId).ToList();

                    // [FIX LỖI 500 TRANSLATION]
                    // BƯỚC 1: Query Database (Chỉ lấy dữ liệu thô, KHÔNG tính toán ngày giờ ở đây)
                    var rawAssignments = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
                        .Include(a => a.Trip)
                        .Where(a =>
                            driverIds.Contains(a.DriverId) &&
                            (a.Trip.Status != TripStatus.COMPLETED && a.Trip.Status != TripStatus.CANCELLED)
                        )
                        // CHỈ SELECT CÁC TRƯỜNG CẦN THIẾT (Projection)
                        .Select(a => new
                        {
                            DriverId = a.DriverId,
                            TripCode = a.Trip.TripCode,
                            // Lấy các mốc thời gian thô về để C# tự tính
                            ActualPickup = a.Trip.ActualPickupTime,
                            Created = a.Trip.CreateAt,
                            ActualEnd = a.Trip.ActualCompletedTime,
                            Duration = a.Trip.ActualDuration
                        })
                        .ToListAsync(); // <--- QUAN TRỌNG: Thực thi SQL ngay tại đây để lấy dữ liệu về RAM

                    // BƯỚC 2: Tính toán và Filter trong bộ nhớ (Client-side Evaluation)
                    // Lúc này .Add() là C# thuần túy nên không bị lỗi
                    var busyAssignments = rawAssignments
                        .Where(x =>
                        {
                            // Logic tính ngày bắt đầu/kết thúc của chuyến đi cũ
                            var tripStart = x.ActualPickup ?? x.Created;
                            var tripEnd = x.ActualEnd ?? tripStart.Add(x.Duration); // .Add() chạy ở đây là OK

                            // Logic trùng lịch: (StartA < EndB) && (EndA > StartB)
                            // A: Chuyến cũ (tripStart, tripEnd)
                            // B: Chuyến mới (start, end)
                            return tripStart < end && tripEnd > start;
                        })
                        .ToList();

                    // C. Map sang DTO hiển thị cho màn hình Detail
                    dto.MyDrivers = myDrivers.Select(d => {
                        // Tìm trong danh sách busyAssignments đã filter ở bước 2
                        var busyInfo = busyAssignments.FirstOrDefault(b => b.DriverId == d.DriverId);

                        bool isBusy = busyInfo != null;
                        bool isOverloaded = !d.CanDrive;

                        string statusMsg = "Sẵn sàng";
                        if (isBusy) statusMsg = $"Bận chuyến {busyInfo.TripCode}";
                        else if (isOverloaded) statusMsg = "Hết giờ lái (Luật 48h)";

                        return new OwnerDriverStatusDTO
                        {
                            DriverId = d.DriverId,
                            FullName = d.FullName,
                            PhoneNumber = d.PhoneNumber,
                            AvatarUrl = d.AvatarUrl,
                            IsAvailable = !isBusy && !isOverloaded,
                            StatusMessage = statusMsg,
                            Stats = $"Hôm nay: {d.HoursDrivenToday}h | Tuần: {d.HoursDrivenThisWeek}h"
                        };
                    }).OrderByDescending(x => x.IsAvailable).ToList();
                }

                return new ResponseDTO("Success", 200, true, dto);
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

        // Trong PostPackageService.cs
        public async Task<ResponseDTO> CalculateAndValidateRouteAsync(RouteCalculationRequestDTO dto)
        {
            try
            {
                // 1. VALIDATE ĐẦU VÀO (Thêm đoạn này để chặn lỗi 500)
                if (dto.StartLocation == null || string.IsNullOrWhiteSpace(dto.StartLocation.Address))
                    return new ResponseDTO("Vui lòng nhập địa chỉ điểm đi.", 400, false);

                if (dto.EndLocation == null || string.IsNullOrWhiteSpace(dto.EndLocation.Address))
                    return new ResponseDTO("Vui lòng nhập địa chỉ điểm đến.", 400, false);


                // 1. Kiểm tra tọa độ (Geocode nếu thiếu)
                // SỬA LỖI: Tạo new Location thay vì gán property
                if (IsLocationMissingCoordinates(dto.StartLocation))
                {
                    var geo = await _vietMapService.GeocodeAsync(dto.StartLocation.Address);
                    if (geo != null)
                    {
                        // SỬA LỖI: Thêm ?? 0
                        dto.StartLocation = new Location(
                            dto.StartLocation.Address,
                            geo.Latitude ?? 0,
                            geo.Longitude ?? 0
                        );
                    }
                }

                if (IsLocationMissingCoordinates(dto.EndLocation))
                {
                    var geo = await _vietMapService.GeocodeAsync(dto.EndLocation.Address);
                    if (geo != null)
                    {
                        // SỬA LỖI: Thêm ?? 0
                        dto.EndLocation = new Location(
                            dto.EndLocation.Address,
                            geo.Latitude ?? 0,
                            geo.Longitude ?? 0
                        );
                    }
                }

                // Nếu vẫn không có tọa độ -> Báo lỗi địa chỉ
                if (IsLocationMissingCoordinates(dto.StartLocation) || IsLocationMissingCoordinates(dto.EndLocation))
                {
                    return new ResponseDTO("Không xác định được tọa độ địa điểm. Vui lòng kiểm tra lại địa chỉ.", 400, false);
                }

                // 2. Gọi Vietmap lấy thông số
                var path = await _vietMapService.GetRouteAsync(dto.StartLocation, dto.EndLocation, "truck");

                if (path == null)
                    return new ResponseDTO("Không tìm thấy lộ trình phù hợp.", 404, false);

                // 3. Tính toán thời gian an toàn
                double rawHours = path.Time / (1000.0 * 60 * 60);
                double safetyFactor = 1.15; // +15% an toàn
                double loadingTime = 0.5;   // +30p bốc dỡ
                double estimatedHours = (rawHours * safetyFactor) + loadingTime;

                estimatedHours = Math.Round(estimatedHours, 1);
                double distanceKm = Math.Round(path.Distance / 1000.0, 2);

                // 4. Tính ngày giao hàng tối thiểu
                var rawMinDate = dto.ExpectedPickupDate.AddHours(estimatedHours);

                var minDeliveryDate = CeilToNextHour(rawMinDate);

                var suggestedDate = minDeliveryDate.AddDays(1);

                var result = new RouteCalculationResultDTO
                {
                    DistanceKm = distanceKm,
                    EstimatedDurationHours = estimatedHours,
                    SuggestedMinDeliveryDate = suggestedDate,
                    IsValid = true,
                    Message = "Lộ trình khả thi."
                };

                // 5. Validate ngày giao (nếu user đã chọn)
                if (dto.ExpectedDeliveryDate.HasValue)
                {
                    if (dto.ExpectedDeliveryDate.Value <= minDeliveryDate)
                    {
                        

                        result.IsValid = false;
                        result.Message = $"Thời gian quá ngắn! Với {distanceKm}km, cần khoảng {estimatedHours}h. " +
                                         $"Gợi ý ngày giao: {suggestedDate:dd/MM/yyyy HH:mm}";
                    }
                }

                return new ResponseDTO("Tính toán thành công", 200, true, result);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi tính toán: {ex.Message}", 500, false);
            }
        }

        // HÀM HELPER LÀM TRÒN (Viết thêm vào cuối class hoặc trong Utility)
        private DateTime CeilToNextHour(DateTime dt)
        {
            // Bỏ giây và mili-giây trước
            var d = dt.AddTicks(-(dt.Ticks % TimeSpan.TicksPerMinute));

            // Nếu phút > 0 thì cộng thêm 1 giờ và reset phút về 0
            if (d.Minute > 0)
            {
                return new DateTime(d.Year, d.Month, d.Day, d.Hour, 0, 0, d.Kind).AddHours(1);
            }

            // Nếu phút = 0 thì giữ nguyên (chỉ reset giây)
            return new DateTime(d.Year, d.Month, d.Day, d.Hour, 0, 0, d.Kind);
        }
    }
}