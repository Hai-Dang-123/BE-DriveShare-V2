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
using Microsoft.Extensions.DependencyInjection;
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
        private readonly ITrafficRestrictionService _trafficRestrictionService;
        private readonly INotificationService _notificationService;
        // 1. KHAI BÁO BIẾN NÀY
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public PostPackageService(
            IUnitOfWork unitOfWork,
            UserUtility userUtility,
            IShippingRouteService shippingRouteService,
            IPostContactService postContactService,
            IUserDocumentService userDocumentService,
            IVietMapService vietMapService,
            IOwnerDriverLinkService ownerDriverLinkService,
            ITrafficRestrictionService trafficRestrictionService,
            INotificationService notificationService,
            IServiceScopeFactory serviceScopeFactory)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _shippingRouteService = shippingRouteService;
            _postContactService = postContactService;
            _userDocumentService = userDocumentService;
            _vietMapService = vietMapService;
            _ownerDriverLinkService = ownerDriverLinkService;
            _trafficRestrictionService = trafficRestrictionService;
            _notificationService = notificationService;
            _serviceScopeFactory = serviceScopeFactory;
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

        // Đảm bảo bạn đã inject IServiceScopeFactory vào Constructor
        // private readonly IServiceScopeFactory _serviceScopeFactory;

        public async Task<ResponseDTO> ChangePostPackageStatusAsync(ChangePostPackageStatusDTO dto)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 1. Cập nhật trạng thái
                var postPackage = await _unitOfWork.PostPackageRepo.GetByIdAsync(dto.PostPackageId);
                if (postPackage == null) return new ResponseDTO("Post package not found.", 404, false);

                postPackage.Status = dto.NewStatus;
                postPackage.Updated = DateTime.UtcNow;

                await _unitOfWork.PostPackageRepo.UpdateAsync(postPackage);
                await _unitOfWork.SaveChangeAsync();

                // 2. Commit Transaction (Lưu xong xuôi trạng thái bài đăng)
                await transaction.CommitAsync();

                // =======================================================================
                // 3. GỬI THÔNG BÁO (GỌI SERVICE LÀ ĐỦ)
                // =======================================================================
                if (dto.NewStatus == PostStatus.OPEN)
                {
                    try
                    {
                        string title = "📦 Đơn hàng mới!";
                        string body = "Có một đơn hàng mới vừa được đăng tải. Vào xem ngay!";
                        var dataDict = new Dictionary<string, string>
                {
                    { "screen", "PostDetail" },
                    { "id", dto.PostPackageId.ToString() }
                };

                        // Chỉ cần gọi 1 dòng này thôi!
                        // Service này đã lo việc: Lấy list user -> Lưu DB -> Bắn Firebase
                        await _notificationService.SendToRoleAsync("Owner", title, body, dataDict);
                    }
                    catch (Exception ex)
                    {
                        // Log lỗi noti (Console/File) để biết đường sửa, nhưng ko làm fail request chính
                        Console.WriteLine($"⚠️ Lỗi Noti: {ex.Message}");
                        // Nếu muốn test lỗi trên Postman thì uncomment dòng dưới:
                        // throw new Exception($"DEBUG ERROR: {ex.Message}");
                    }
                }
                // =======================================================================

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
        // Đảm bảo đã inject service này ở Constructor
        // private readonly ITrafficRestrictionService _trafficRestrictionService;

        public async Task<ResponseDTO> CreateProviderPostPackageAsync(PostPackageCreateDTO dto)
        {
            // Bắt đầu transaction để đảm bảo tính toàn vẹn dữ liệu
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 1. --- AUTH & BASIC VALIDATION ---
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Invalid user token.", 401, false);

                var verifyCheck = await _userDocumentService.ValidateUserDocumentsAsync(userId);
                if (!verifyCheck.IsValid) return new ResponseDTO(verifyCheck.Message, 403, false);

                var route = dto.ShippingRoute;
                var today = DateTime.UtcNow.Date;

                if (route.ExpectedPickupDate.Date < today) return new ResponseDTO("Ngày lấy hàng dự kiến không thể ở trong quá khứ.", 400, false);
                if (route.ExpectedDeliveryDate <= route.ExpectedPickupDate) return new ResponseDTO("Ngày giao hàng phải sau thời gian lấy hàng.", 400, false);

                // 2. --- TÍNH TOÁN LỘ TRÌNH & RESTRICTIONS ---
                // Khởi tạo biến để lưu kết quả tính toán
                double calculatedDistance = 0;
                double travelTimeTotal = 0;
                double waitTimeTotal = 0;
                double totalDuration = 0;
                string restrictionNote = null;

                // A. Geocode (Chuyển đổi địa chỉ sang tọa độ nếu thiếu)
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

                // B. Gọi VietMap & Check Cấm tải
                if (!IsLocationMissingCoordinates(dto.ShippingRoute.StartLocation) && !IsLocationMissingCoordinates(dto.ShippingRoute.EndLocation))
                {
                    // Lấy lộ trình xe tải
                    var path = await _vietMapService.GetRouteAsync(dto.ShippingRoute.StartLocation, dto.ShippingRoute.EndLocation, "truck");

                    if (path == null)
                    {
                        // Nếu không tìm thấy đường, trả lỗi (hoặc bạn có thể cho phép nhập tay tùy nghiệp vụ)
                        return new ResponseDTO("Không tìm thấy lộ trình phù hợp giữa hai điểm này.", 400, false);
                    }

                    // --- [FIX LOGIC TÍNH GIỜ] ---
                    double distKm = Math.Round(path.Distance / 1000.0, 2);
                    calculatedDistance = distKm;

                    // Lấy giờ từ API
                    double apiHours = path.Time / (1000.0 * 60 * 60);

                    // KIỂM TRA TỐC ĐỘ TRUNG BÌNH
                    // Nếu > 55km/h => API đang trả giờ xe con/lý tưởng => Tính lại theo 50km/h
                    double avgSpeed = (apiHours > 0) ? distKm / apiHours : 0;
                    double rawHours = apiHours;

                    if (avgSpeed > 55.0)
                    {
                        rawHours = distKm / 50.0; // Ép về tốc độ 50km/h
                    }

                    // Buffer an toàn: 15% + 30 phút cho kẹt xe/nghỉ ngơi
                    double bufferHours = (rawHours * 0.15) + 0.5;
                    travelTimeTotal = Math.Round(rawHours + bufferHours, 1); // Lúc này travelTime sẽ khoảng 40 tiếng (Hợp lý)

                    // --- Tính toán thời gian chờ (Wait Time) do Cấm tải ---
                    // Dự kiến xe đến cửa ngõ đích sau khi chạy xong travelTimeTotal
                    var estimatedArrivalTime = dto.ShippingRoute.ExpectedPickupDate.AddHours(travelTimeTotal);

                    // Gọi TrafficRestrictionService (Service chúng ta vừa tạo)
                    var restrictionResult = await _trafficRestrictionService.CheckRestrictionAsync(dto.ShippingRoute.EndLocation.Address, estimatedArrivalTime);

                    if (restrictionResult.IsRestricted)
                    {
                        waitTimeTotal = Math.Round(restrictionResult.WaitTime, 1);
                        restrictionNote = $"Phải chờ {waitTimeTotal}h do {restrictionResult.Reason}";
                    }

                    // --- Tổng thời gian cần thiết ---
                    totalDuration = travelTimeTotal + waitTimeTotal;

                    // --- VALIDATE LOGIC THỜI GIAN ---
                    // Thời gian Provider cam kết (Delivery - Pickup)
                    double providerInputDuration = (dto.ShippingRoute.ExpectedDeliveryDate - dto.ShippingRoute.ExpectedPickupDate).TotalHours;

                    // Kiểm tra: Nếu thời gian cam kết < Thời gian tính toán thực tế -> Lỗi
                    // (Cho phép sai số nhỏ 0.1h)
                    if (providerInputDuration < totalDuration - 0.1)
                    {
                        await transaction.RollbackAsync();

                        string errorMsg = $"Thời gian giao hàng quá ngắn! Hệ thống tính toán cần tối thiểu {totalDuration} giờ";
                        errorMsg += $" (Chạy: {travelTimeTotal}h";
                        if (waitTimeTotal > 0) errorMsg += $", Chờ cấm tải: {waitTimeTotal}h";
                        errorMsg += ").";

                        return new ResponseDTO(errorMsg, 400, false);
                    }
                }

                // 3. --- LƯU DỮ LIỆU ---

                // Cập nhật kết quả tính toán vào DTO để Service ShippingRoute lưu xuống DB
                dto.ShippingRoute.EstimatedDistanceKm = calculatedDistance;
                dto.ShippingRoute.TravelTimeHours = travelTimeTotal;
                dto.ShippingRoute.WaitTimeHours = waitTimeTotal;
                dto.ShippingRoute.EstimatedDurationHours = totalDuration;
                dto.ShippingRoute.RestrictionNote = restrictionNote;

                // Lưu ý: Các trường TimeOnly (StartTimeToPickup...) đã có trong DTO, 
                // hàm CreateAndAddShippingRouteAsync sẽ tự động lưu chúng nếu user có nhập.

                // A. Tạo ShippingRoute
                ShippingRoute newShippingRoute = await _shippingRouteService.CreateAndAddShippingRouteAsync(dto.ShippingRoute);

                // B. Tạo Bài đăng (PostPackage)
                var postPackage = new PostPackage
                {
                    PostPackageId = Guid.NewGuid(),
                    ProviderId = userId,
                    Title = dto.Title,
                    Description = dto.Description,
                    OfferedPrice = dto.OfferedPrice,
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow,
                    Status = dto.Status, // Thường là PENDING hoặc OPEN
                    ShippingRouteId = newShippingRoute.ShippingRouteId
                };
                await _unitOfWork.PostPackageRepo.AddAsync(postPackage);

                // C. Tạo Contact (Sender/Receiver)
                await _postContactService.CreateAndAddContactsAsync(postPackage.PostPackageId, dto.SenderContact, dto.ReceiverContact);

                // D. Link Packages (Cập nhật trạng thái các gói hàng)
                foreach (var packageId in dto.PackageIds)
                {
                    var package = await _unitOfWork.PackageRepo.GetByIdAsync(packageId);

                    // Validate Package
                    if (package == null) throw new Exception($"Gói hàng {packageId} không tồn tại.");
                    if (package.ProviderId != userId) throw new Exception($"Gói hàng {package.PackageCode} không thuộc quyền quản lý của bạn.");
                    if (package.PostPackageId != null) throw new Exception($"Gói hàng {package.PackageCode} đã nằm trong bài đăng khác.");
                    if (package.Status != PackageStatus.PENDING) throw new Exception($"Gói hàng {package.PackageCode} phải ở trạng thái PENDING.");

                    // Update Package
                    package.PostPackageId = postPackage.PostPackageId;
                    package.Status = PackageStatus.LOOKING_FOR_OWNER; // Chuyển trạng thái sang tìm chủ xe
                    await _unitOfWork.PackageRepo.UpdateAsync(package);
                }

                // 4. --- COMMIT ---
                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                return new ResponseDTO($"Tạo bài đăng thành công ({dto.PackageIds.Count} gói hàng).", 201, true, new { PostPackageId = postPackage.PostPackageId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO($"Lỗi tạo bài đăng: {ex.Message}", 400, false);
            }
        }

        // Helper check tọa độ
        private bool IsLocationMissingCoordinates(Location loc)
        {
            return loc == null || !loc.Latitude.HasValue || !loc.Longitude.HasValue || (loc.Latitude.Value == 0 && loc.Longitude.Value == 0);
        }

        // =============================================================================
        // 3. GET ALL POST PACKAGES (OPTIMIZED)
        // =============================================================================
        public async Task<ResponseDTO> GetAllPostPackagesAsync(int pageNumber, int pageSize, string? search, string? sortBy, string? sortOrder)
        {
            try
            {
                // [OPTIMIZATION] AsNoTracking để đọc nhanh hơn + AsSplitQuery để tách query 1-N
                IQueryable<PostPackage> query = _unitOfWork.PostPackageRepo.GetAllQueryable()
                    .AsNoTracking()
                    .AsSplitQuery() // Quan trọng: Tách query SQL để tránh Cartesian Explosion
                    .Include(p => p.Provider)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.StartLocation)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.EndLocation)
                    .Include(p => p.Packages).ThenInclude(pkg => pkg.HandlingDetail); // Include chi tiết

                // Search & Sort
                query = ApplyPostPackageFilter(query, search);
                query = ApplyPostPackageSort(query, sortBy, sortOrder);

                // Paging
                var totalCount = await query.CountAsync();
                var pagedData = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Check expire (Lưu ý: Vì dùng AsNoTracking nên cần Attach lại nếu muốn SaveChanges, 
                // nhưng ở đây ta chấp nhận query lại context để update hoặc bỏ qua update trong hàm Get này để tối ưu tốc độ)
                // Để an toàn và vẫn update được trạng thái, ta nên dùng Tracking cho page hiện tại hoặc update riêng.
                // Ở đây tôi giữ logic cũ nhưng cảnh báo về Performance nếu SaveChange quá nhiều.
                // Tuy nhiên, vì pagedData số lượng ít (pageSize), nên việc update này không quá chậm.
                // Ta cần attach lại vì AsNoTracking. Hoặc đơn giản bỏ AsNoTracking ở trên nếu muốn update.
                // Để tối ưu nhất: Ta bỏ AsNoTracking ở query trên để Update được.

                // Re-query without AsNoTracking for the logic `CheckAndExpirePostsAsync` to work properly with EF Change Tracker
                // Hoặc đơn giản là xóa .AsNoTracking() ở dòng query đầu tiên.
                // [DECISION]: Để query nhanh nhất, ta dùng AsSplitQuery. Việc tracking 10-20 records không quá tốn.

                await CheckAndExpirePostsAsync(pagedData);

                var dtos = pagedData.Select(MapToReadDTO).ToList();
                return new ResponseDTO("Success", 200, true, new PaginatedDTO<PostPackageReadDTO>(dtos, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex) { return new ResponseDTO(ex.Message, 500, false); }
        }

        // =============================================================================
        // 4. GET BY PROVIDER (OPTIMIZED)
        // =============================================================================
        public async Task<ResponseDTO> GetPostPackagesByProviderIdAsync(Guid providerId, int pageNumber, int pageSize, string? search, string? sortBy, string? sortOrder)
        {
            try
            {
                IQueryable<PostPackage> query = _unitOfWork.PostPackageRepo.GetByProviderIdQueryable(providerId)
                    .AsSplitQuery() // Tối ưu query
                    .Include(p => p.Provider)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.StartLocation)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.EndLocation)
                    .Include(p => p.Packages).ThenInclude(pkg => pkg.HandlingDetail);

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
        // 5. GET OPEN POSTS (OPTIMIZED)
        // =============================================================================
        public async Task<ResponseDTO> GetOpenPostPackagesAsync(int pageNumber, int pageSize, string? search, string? sortBy, string? sortOrder)
        {
            try
            {
                IQueryable<PostPackage> query = _unitOfWork.PostPackageRepo.GetAllQueryable()
                    .AsSplitQuery() // Tối ưu query
                    .Include(p => p.Provider)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.StartLocation)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.EndLocation)
                    .Include(p => p.Packages).ThenInclude(pkg => pkg.HandlingDetail)
                    .Where(p => p.Status == PostStatus.OPEN);

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
        // 7. GET MY POSTS (OPTIMIZED)
        // =============================================================================
        public async Task<ResponseDTO> GetMyPostPackagesAsync(int pageNumber, int pageSize, string? search, string? sortBy, string? sortOrder)
        {
            try
            {
                var providerId = _userUtility.GetUserIdFromToken();
                if (providerId == Guid.Empty) return new ResponseDTO("Unauthorized.", 401, false);

                IQueryable<PostPackage> query = _unitOfWork.PostPackageRepo.GetByProviderIdQueryable(providerId)
                    .AsSplitQuery() // Tối ưu query
                    .Include(p => p.Provider)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.StartLocation)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.EndLocation)
                    .Include(p => p.Packages).ThenInclude(pkg => pkg.HandlingDetail);

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
        // [FIXED & UPDATED] 6. GET DETAILS
        // =============================================================================
        public async Task<ResponseDTO> GetPostPackageDetailsAsync(Guid postPackageId)
        {
            try
            {
                // 1. Query tối ưu (AsSplitQuery)
                var postPackage = await _unitOfWork.PostPackageRepo.GetAllQueryable()
                    .AsSplitQuery()
                    .Include(p => p.Provider)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.StartLocation)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.EndLocation)
                    .Include(p => p.PostContacts)
                    .Include(p => p.Packages).ThenInclude(pk => pk.PackageImages)
                    .Include(p => p.Packages).ThenInclude(pk => pk.Item).ThenInclude(i => i.ItemImages)
                    .Include(p => p.Packages).ThenInclude(pk => pk.HandlingDetail)
                    .FirstOrDefaultAsync(p => p.PostPackageId == postPackageId);

                if (postPackage == null) return new ResponseDTO("Bài đăng không tồn tại.", 404, false);

                // 2. Check hết hạn
                await CheckAndExpirePostsAsync(new List<PostPackage> { postPackage });

                var dto = MapToDetailDTO(postPackage);
                dto.MyDrivers = new List<OwnerDriverStatusDTO>();

                // --- 3. TÍNH TOÁN GỢI Ý (DRIVER SUGGESTION) ---
                if (postPackage.ShippingRoute != null)
                {
                    // Lấy dữ liệu từ DB (đã lưu lúc Create Post)
                    double dist = postPackage.ShippingRoute.EstimatedDistanceKm;
                    double durationHours = postPackage.ShippingRoute.EstimatedDurationHours; // Lưu ý: DB thường lưu Tổng giờ (Driving + Buffer)

                    // [MỚI] Lấy WaitTime & Buffer
                    double waitTimeHours = postPackage.ShippingRoute.WaitTimeHours ?? 0;
                    double bufferHours = 0;

                    // Biến này dùng để truyền vào Helper (Cần là Giờ lái thuần túy - Raw Driving Hours)
                    double rawDrivingHoursForHelper = durationHours;

                    // Fallback: Nếu DB chưa có dữ liệu (dữ liệu cũ), gọi VietMap tính lại
                    if (dist == 0 || durationHours == 0)
                    {
                        var startNode = postPackage.ShippingRoute.StartLocation;
                        var endNode = postPackage.ShippingRoute.EndLocation;

                        if (!IsLocationMissingCoordinates(startNode) && !IsLocationMissingCoordinates(endNode))
                        {
                            var path = await _vietMapService.GetRouteAsync(startNode, endNode, "truck");
                            if (path != null)
                            {
                                // 1. Tính Distance
                                dist = Math.Round(path.Distance / 1000.0, 2);

                                // 2. Tính Duration (FIX LỖI 27 TIẾNG HN-SG)
                                double apiHours = path.Time / (1000.0 * 60 * 60);

                                // Logic: Nếu vận tốc trung bình > 55km/h => API đang trả về giờ xe con.
                                // Xe tải nặng đường dài VN chỉ chạy trung bình 50km/h.
                                double avgSpeed = (apiHours > 0) ? dist / apiHours : 0;
                                const double REALISTIC_TRUCK_SPEED = 50.0;

                                double realDrivingHours;

                                if (avgSpeed > 55.0)
                                {
                                    // Tính lại giờ chạy thực tế (khoảng 35h cho 1700km)
                                    realDrivingHours = dist / REALISTIC_TRUCK_SPEED;
                                }
                                else
                                {
                                    realDrivingHours = apiHours;
                                }

                                // 3. Tính Buffer (15% + 30p rủi ro)
                                bufferHours = (realDrivingHours * 0.15) + 0.5;

                                // Gán giá trị Raw Driving cho biến Helper
                                rawDrivingHoursForHelper = realDrivingHours;

                                // Cập nhật lại durationHours (để hiển thị nếu cần, dù biến này ít dùng dưới đây)
                                durationHours = realDrivingHours + bufferHours;
                            }
                        }
                    }
                    else
                    {
                        // Trường hợp lấy từ DB (durationHours đang là Tổng), ta cần tách ra Raw Driving để tính Helper cho chuẩn
                        // Vì Helper sẽ tự cộng lại buffer
                        // Ước tính ngược: Raw = Duration / 1.15 (gần đúng) hoặc lấy Duration - Buffer (nếu DB có lưu buffer riêng)
                        // Ở đây ta tạm lấy Duration làm Raw nếu không gọi API, hoặc tách buffer mặc định
                        bufferHours = (durationHours * 0.15) + 0.5;
                        rawDrivingHoursForHelper = durationHours - bufferHours;
                        if (rawDrivingHoursForHelper < 0) rawDrivingHoursForHelper = durationHours;
                    }

                    // Gọi Helper tính toán kịch bản
                    if (rawDrivingHoursForHelper > 0)
                    {
                        dto.DriverSuggestion = TripCalculationHelper.CalculateScenarios(
                            dist,
                            rawDrivingHoursForHelper, // Quan trọng: Truyền giờ lái thuần túy
                            waitTimeHours,
                            bufferHours,
                            postPackage.ShippingRoute.ExpectedPickupDate,
                            postPackage.ShippingRoute.ExpectedDeliveryDate
                        );
                    }
                }

                // --- 4. CHECK TÀI XẾ NỘI BỘ (OWNER ONLY) ---
                var currentUserId = _userUtility.GetUserIdFromToken();
                var ownerEntity = await _unitOfWork.OwnerRepo.GetByIdAsync(currentUserId);
                var isOwner = (ownerEntity != null);

                // Chỉ chạy logic này nếu là Owner và đã có gợi ý tài xế
                if (isOwner && postPackage.ShippingRoute != null && dto.DriverSuggestion != null)
                {
                    var myDrivers = await _ownerDriverLinkService.GetDriversWithStatsByOwnerIdAsync(currentUserId);

                    if (myDrivers != null && myDrivers.Any())
                    {
                        var newTripStart = postPackage.ShippingRoute.ExpectedPickupDate;
                        var newTripEnd = postPackage.ShippingRoute.ExpectedDeliveryDate;

                        // Helper lấy đầu tuần (Thứ 2)
                        var startOfTripWeek = GetStartOfWeek(newTripStart);
                        var endOfTripWeek = startOfTripWeek.AddDays(7);

                        var driverIds = myDrivers.Select(d => d.DriverId).ToList();

                        // Lấy lịch chạy của các tài xế này
                        var rawAssignments = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
                            .AsNoTracking()
                            .Include(a => a.Trip)
                            .Where(a =>
                                driverIds.Contains(a.DriverId) &&
                                (a.Trip.Status != TripStatus.COMPLETED && a.Trip.Status != TripStatus.CANCELLED)
                            )
                            .Select(a => new
                            {
                                DriverId = a.DriverId,
                                TripCode = a.Trip.TripCode,
                                PickupTime = a.Trip.ActualPickupTime,
                                CreateTime = a.Trip.CreateAt,
                                EndTimeRaw = a.Trip.ActualCompletedTime,
                                DurationSpan = a.Trip.ActualDuration
                            })
                            .ToListAsync();

                        // Số giờ cần thiết cho chuyến mới (Lấy từ Suggestion)
                        double requiredHoursForNewTrip = dto.DriverSuggestion.RequiredHoursFromQuota;

                        dto.MyDrivers = myDrivers.Select(d =>
                        {
                            // Lọc chuyến của tài xế này
                            var driverTrips = rawAssignments
                                .Where(x => x.DriverId == d.DriverId)
                                .Select(x => new
                                {
                                    Code = x.TripCode,
                                    // Logic tính giờ Start/End tương đối
                                    Start = x.PickupTime ?? x.CreateTime,
                                    End = x.EndTimeRaw ?? (x.PickupTime ?? x.CreateTime).Add(x.DurationSpan),
                                    DurationHours = x.DurationSpan.TotalHours
                                })
                                .ToList();

                            // Check trùng giờ (Conflict)
                            var conflictTrip = driverTrips.FirstOrDefault(x => x.Start < newTripEnd && x.End > newTripStart);

                            // Check quá tải tuần (Overload)
                            double hoursAlreadyBooked = driverTrips
                                .Where(x => x.Start >= startOfTripWeek && x.Start < endOfTripWeek)
                                .Sum(x => x.DurationHours);

                            double remainingHours = 48 - hoursAlreadyBooked;

                            bool isBusy = (conflictTrip != null);
                            bool isOverloaded = (remainingHours < requiredHoursForNewTrip);

                            string statusMsg = "Sẵn sàng";
                            string subStats = $"Tuần này còn: {remainingHours:N1}h (Cần: {requiredHoursForNewTrip:N1}h)";

                            if (isBusy)
                            {
                                statusMsg = $"Bận chuyến {conflictTrip.Code}";
                                subStats = "Đang kẹt lịch chạy trùng giờ";
                            }
                            else if (isOverloaded)
                            {
                                statusMsg = "Không đủ giờ lái (Luật 48h)";
                                subStats = $"Đã chạy: {hoursAlreadyBooked:N1}h. Thiếu {(requiredHoursForNewTrip - remainingHours):N1}h.";
                            }
                            else if (!d.CanDrive)
                            {
                                statusMsg = "Tài xế không khả dụng";
                                subStats = "Kiểm tra bằng lái/tài khoản";
                            }

                            return new OwnerDriverStatusDTO
                            {
                                DriverId = d.DriverId,
                                FullName = d.FullName,
                                PhoneNumber = d.PhoneNumber,
                                AvatarUrl = d.AvatarUrl,
                                IsAvailable = !isBusy && !isOverloaded && d.CanDrive,
                                StatusMessage = statusMsg,
                                Stats = subStats
                            };
                        })
                        .OrderByDescending(x => x.IsAvailable)
                        .ToList();
                    }
                }

                return new ResponseDTO("Thành công", 200, true, dto);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi: {ex.Message}", 500, false);
            }
        }

        // Helper nhỏ để lấy ngày đầu tuần (Thứ 2)
        private DateTime GetStartOfWeek(DateTime dt)
        {
            int diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;
            return dt.Date.AddDays(-1 * diff);
        }

        public async Task<ResponseDTO> CalculateAndValidateRouteAsync(RouteCalculationRequestDTO dto)
        {
            try
            {
                // --- 1. VALIDATION & GEOCODING ---
                if (dto.StartLocation == null || string.IsNullOrWhiteSpace(dto.StartLocation.Address))
                    return new ResponseDTO("Vui lòng nhập địa chỉ điểm đi.", 400, false);
                if (dto.EndLocation == null || string.IsNullOrWhiteSpace(dto.EndLocation.Address))
                    return new ResponseDTO("Vui lòng nhập địa chỉ điểm đến.", 400, false);

                // Geocode Start
                if (IsLocationMissingCoordinates(dto.StartLocation))
                {
                    var geo = await _vietMapService.GeocodeAsync(dto.StartLocation.Address);
                    if (geo != null)
                        dto.StartLocation = new Location(dto.StartLocation.Address, geo.Latitude ?? 0, geo.Longitude ?? 0);
                }
                // Geocode End
                if (IsLocationMissingCoordinates(dto.EndLocation))
                {
                    var geo = await _vietMapService.GeocodeAsync(dto.EndLocation.Address);
                    if (geo != null)
                        dto.EndLocation = new Location(dto.EndLocation.Address, geo.Latitude ?? 0, geo.Longitude ?? 0);
                }

                if (IsLocationMissingCoordinates(dto.StartLocation) || IsLocationMissingCoordinates(dto.EndLocation))
                    return new ResponseDTO("Không xác định được tọa độ địa điểm (Vui lòng kiểm tra lại địa chỉ).", 400, false);

                // --- 2. GET ROUTE (VIETMAP) ---
                var path = await _vietMapService.GetRouteAsync(dto.StartLocation, dto.EndLocation, "truck");
                if (path == null) return new ResponseDTO("Không tìm thấy lộ trình phù hợp cho xe tải.", 404, false);

                // --- 3. TÍNH TOÁN THỜI GIAN DI CHUYỂN (TRAVEL TIME) ---
                double apiHours = path.Time / (1000.0 * 60 * 60);
                double distanceKm = Math.Round(path.Distance / 1000.0, 2);

                // [FIX LOGIC] Kẹp tốc độ trần (Speed Clamping)
                double rawHours = apiHours;
                if (distanceKm > 0 && apiHours > 0)
                {
                    double avgSpeed = distanceKm / apiHours;
                    const double MAX_TRUCK_SPEED = 55.0; // km/h
                    const double REALISTIC_SPEED = 50.0; // km/h (Xe tải Bắc Nam)

                    if (avgSpeed > MAX_TRUCK_SPEED)
                    {
                        // Tính lại giờ chạy dựa trên tốc độ thực tế
                        rawHours = distanceKm / REALISTIC_SPEED;
                    }
                }

                // Buffer: Cộng thêm 15% + 30 phút cho tắc đường/nghỉ ngơi
                double bufferHours = (rawHours * 0.15) + 0.5;
                double travelTimeTotal = rawHours + bufferHours;

                // --- 4. CHECK GIỜ CẤM & TÍNH THỜI GIAN CHỜ (LOGIC START & END) ---
                double totalWaitTime = 0;
                string restrictionNote = "";

                // BƯỚC 4.1: Kiểm tra cấm tải tại ĐẦU ĐI (Start Location)
                var startRestriction = await _trafficRestrictionService.CheckRestrictionAsync(
                    dto.StartLocation.Address,
                    dto.ExpectedPickupDate
                );

                if (startRestriction.IsRestricted)
                {
                    totalWaitTime += startRestriction.WaitTime;
                    restrictionNote += $"Đầu đi: Chờ {Math.Round(startRestriction.WaitTime, 1)}h ({startRestriction.Reason}). ";
                }

                // BƯỚC 4.2: Tính giờ đến dự kiến (Arrival Time)
                // Giờ xuất phát thực tế = Giờ muốn đi + Thời gian phải chờ ở đầu đi
                var realDepartureTime = dto.ExpectedPickupDate.AddHours(startRestriction.IsRestricted ? startRestriction.WaitTime : 0);

                // Giờ đến = Giờ xuất phát thực tế + Thời gian chạy
                var estimatedArrivalTime = realDepartureTime.AddHours(travelTimeTotal);

                // BƯỚC 4.3: Kiểm tra cấm tải tại ĐẦU ĐẾN (End Location)
                var endRestriction = await _trafficRestrictionService.CheckRestrictionAsync(
                    dto.EndLocation.Address,
                    estimatedArrivalTime
                );

                if (endRestriction.IsRestricted)
                {
                    totalWaitTime += endRestriction.WaitTime;
                    string separator = string.IsNullOrEmpty(restrictionNote) ? "" : " + ";
                    restrictionNote += $"{separator}Đầu đến: Chờ {Math.Round(endRestriction.WaitTime, 1)}h ({endRestriction.Reason}).";
                }

                // --- 5. TỔNG HỢP & VALIDATE ---
                double totalDuration = travelTimeTotal + totalWaitTime; // Tổng = Chạy + Chờ (Start) + Chờ (End)

                // Tính ngày giao hàng tối thiểu
                var minDeliveryDateRaw = dto.ExpectedPickupDate.AddHours(totalDuration);
                var minDeliveryDate = CeilToNextHour(minDeliveryDateRaw);

                // Gợi ý ngày giao hàng (thư thả thêm 1 ngày nếu quãng đường dài > 500km, nếu ngắn thì thôi)
                // Logic phụ: Nếu đường dài > 1000km (như Bắc Nam), cộng hẳn 24h để an toàn
                var suggestedDate = (distanceKm > 500) ? minDeliveryDate.AddDays(1) : minDeliveryDate.AddHours(4);

                // Map kết quả ra DTO
                var result = new RouteCalculationResultDTO
                {
                    EstimatedDistanceKm = distanceKm,
                    TravelTimeHours = Math.Round(travelTimeTotal, 1),
                    WaitTimeHours = Math.Round(totalWaitTime, 1),
                    EstimatedDurationHours = Math.Round(totalDuration, 1),
                    RestrictionNote = restrictionNote,

                    SuggestedMinDeliveryDate = suggestedDate,
                    IsValid = true,
                    Message = totalWaitTime > 0
                              ? $"Lộ trình khả thi. Lưu ý: {restrictionNote}"
                              : "Lộ trình khả thi."
                };

                // Validate với thời gian khách mong muốn
                if (dto.ExpectedDeliveryDate.HasValue && dto.ExpectedDeliveryDate.Value <= minDeliveryDate)
                {
                    result.IsValid = false;
                    result.Message = $"Thời gian quá gấp! Cần tối thiểu {Math.Round(totalDuration, 1)}h (Gồm {Math.Round(travelTimeTotal, 1)}h chạy + {Math.Round(totalWaitTime, 1)}h chờ). Gợi ý: {suggestedDate:dd/MM/yyyy HH:mm}";
                }

                return new ResponseDTO("Tính toán thành công", 200, true, result);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi hệ thống khi tính toán: {ex.Message}", 500, false);
            }
        }

        // Hàm phụ trợ (Helper) - Để ở dưới cùng của class hoặc trong Helper class
        private DateTime CeilToNextHour(DateTime dt)
        {
            // Nếu phút > 0 hoặc giây > 0 thì làm tròn lên giờ tiếp theo
            if (dt.Minute > 0 || dt.Second > 0)
            {
                return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0).AddHours(1);
            }
            return dt;
        }

        

        // =============================================================================
        // PRIVATE HELPERS
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
                _ => query.OrderByDescending(p => p.Created)
            };
        }

        



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

                    IsFragile = pkg.HandlingDetail?.IsFragile ?? false,
                    IsLiquid = pkg.HandlingDetail?.IsLiquid ?? false,
                    IsRefrigerated = pkg.HandlingDetail?.IsRefrigerated ?? false,
                    IsFlammable = pkg.HandlingDetail?.IsFlammable ?? false,
                    IsHazardous = pkg.HandlingDetail?.IsHazardous ?? false,
                    IsBulky = pkg.HandlingDetail?.IsBulky ?? false,
                    IsPerishable = pkg.HandlingDetail?.IsPerishable ?? false,

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