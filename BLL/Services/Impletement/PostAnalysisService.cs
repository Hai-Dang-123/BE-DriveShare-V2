using BLL.Services.Interface;
using Common.DTOs;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class PostAnalysisService : IPostAnalysisService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAIService _aiService; // Service gọi OpenAI (đã viết ở bài trước)
        private readonly IPostPackageService _postPackageService; // Để lấy data detail
        private readonly IPostTripService _postTripService;       // Để lấy data detail

        public PostAnalysisService(
            IUnitOfWork unitOfWork,
            IAIService aiService,
            IPostPackageService postPackageService,
            IPostTripService postTripService)
        {
            _unitOfWork = unitOfWork;
            _aiService = aiService;
            _postPackageService = postPackageService;
            _postTripService = postTripService;
        }

        public async Task<ResponseDTO> GetOrGeneratePackageAnalysisAsync(Guid postPackageId)
        {
            try
            {
                // BƯỚC 1: LẤY ENTITY TỪ DB (KÈM INCLUDE ĐẦY ĐỦ ĐỂ CÓ DỮ LIỆU GỬI AI)
                // Lưu ý: Phải Include y chang như bên Service kia mới đủ thông tin
                var entity = await _unitOfWork.PostPackageRepo.GetAll()
                    .Include(p => p.Provider)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.StartLocation)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.EndLocation)
                    .Include(p => p.Packages).ThenInclude(pkg => pkg.PackageImages)
                    .Include(p => p.Packages).ThenInclude(pkg => pkg.Item).ThenInclude(i => i.ItemImages)
                    .Include(p => p.PostContacts)
                    // Quan trọng: Không được filter ẩn (IgnoreQueryFilters nếu cần thiết)
                    .Where(p => p.PostPackageId == postPackageId)
                    .FirstOrDefaultAsync();

                if (entity == null)
                    return new ResponseDTO("Bài đăng không tồn tại (hoặc đã bị xóa).", 404, false);

                // BƯỚC 2: CHECK CACHE JSON
                if (!string.IsNullOrEmpty(entity.AIAnalysisJson))
                {
                    try
                    {
                        var cachedResult = JsonSerializer.Deserialize<AIAnalysisResult>(entity.AIAnalysisJson);
                        if (cachedResult != null)
                            return new ResponseDTO("Lấy kết quả phân tích từ Cache thành công.", 200, true, cachedResult);
                    }
                    catch { /* Lỗi JSON thì bỏ qua */ }
                }

                // BƯỚC 3: MAP ENTITY -> DTO (OBJECT) ĐỂ GỬI AI
                // (Thay vì gọi Service kia, ta tự map tại đây)
                var dataForAI = MapEntityToDataForAI(entity);

                // BƯỚC 4: GỌI AI PHÂN TÍCH
                var aiResponse = await _aiService.AnalyzePostPackageAsync(dataForAI);

                if (!aiResponse.IsSuccess)
                    return new ResponseDTO("Lỗi AI: " + aiResponse.RawContent, 500, false);

                // BƯỚC 5: LƯU CACHE VÀO DB
                // Vì biến 'entity' đang được Tracking bởi EF Core (do lấy từ GetAll),
                // nên ta chỉ cần gán giá trị và SaveChanges.
                entity.AIAnalysisJson = JsonSerializer.Serialize(aiResponse.Result);

                await _unitOfWork.PostPackageRepo.UpdateAsync(entity);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Phân tích AI thành công.", 200, true, aiResponse.Result);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi hệ thống: {ex.Message}", 500, false);
            }
        }

        // =========================================================================
        // HELPER MAPPER (PRIVATE) - COPY LOGIC TỪ POSTPACKAGESERVICE SANG ĐÂY
        // =========================================================================
        private object MapEntityToDataForAI(PostPackage p)
        {
            // Ta map sang một Anonymous Object hoặc DTO cụ thể. 
            // Ở đây dùng DTO PostPackageDetailDTO cho chuẩn form.
            return new PostPackageDetailDTO
            {
                PostPackageId = p.PostPackageId,
                Title = p.Title,
                Description = p.Description,
                OfferedPrice = p.OfferedPrice,
                Status = p.Status.ToString(),
                Created = p.Created,

                // Map Provider
                Provider = p.Provider == null ? null : new ProviderInfoInPostDTO
                {
                    ProviderId = p.Provider.UserId,
                    FullName = p.Provider.FullName,
                    //AvatarUrl = p.Provider.AvatarUrl, // AI không cần Avatar
                    PhoneNumber = p.Provider.PhoneNumber
                },

                // Map Route (Quan trọng cho AI tính toán)
                ShippingRoute = p.ShippingRoute == null ? null : new ShippingRouteInPostDTO
                {
                    //ShippingRouteId = p.ShippingRoute.ShippingRouteId,
                    StartLocation = p.ShippingRoute.StartLocation, // Cần địa chỉ/tọa độ
                    EndLocation = p.ShippingRoute.EndLocation,     // Cần địa chỉ/tọa độ
                    ExpectedPickupDate = p.ShippingRoute.ExpectedPickupDate,
                    ExpectedDeliveryDate = p.ShippingRoute.ExpectedDeliveryDate,
                    //PickupTimeWindow = p.ShippingRoute.PickupTimeWindow,
                    //DeliveryTimeWindow = p.ShippingRoute.DeliveryTimeWindow
                },

                // Map Packages (Quan trọng cho AI check trọng lượng/thể tích)
                Packages = p.Packages?.Select(pkg => new PackageForPostDTO
                {
                    //PackageId = pkg.PackageId,
                    //PackageCode = pkg.PackageCode,
                    Title = pkg.Title,
                    WeightKg = pkg.WeightKg,
                    VolumeM3 = pkg.VolumeM3,
                    //Status = pkg.Status.ToString(),

                    // Map Item bên trong
                    Item = pkg.Item == null ? null : new ItemForPackageInPostDTO
                    {
                        ItemName = pkg.Item.ItemName,
                        Description = pkg.Item.Description,
                        DeclaredValue = pkg.Item.DeclaredValue,
                        Currency = pkg.Item.Currency,
                        //Status = pkg.Item.Status.ToString()
                    }
                }).ToList() ?? new List<PackageForPostDTO>()
            };
        }

        // ====================================================================
        // 2. PHÂN TÍCH POST TRIP (Tương tự)
        // ====================================================================
        public async Task<ResponseDTO> GetOrGenerateTripAnalysisAsync(Guid postTripId)
        {
            try
            {
                // BƯỚC 1: Lấy Entity từ DB (Kèm Include đầy đủ)
                var entity = await _unitOfWork.PostTripRepo.GetAll()
                    .Include(p => p.Owner)
                    .Include(p => p.Trip).ThenInclude(t => t.ShippingRoute).ThenInclude(sr => sr.StartLocation)
                    .Include(p => p.Trip).ThenInclude(t => t.ShippingRoute).ThenInclude(sr => sr.EndLocation)
                    .Include(p => p.Trip).ThenInclude(t => t.Vehicle).ThenInclude(v => v.VehicleType)
                    .Include(p => p.Trip).ThenInclude(t => t.Packages)
                    .Include(p => p.PostTripDetails)
                    .Where(p => p.PostTripId == postTripId)
                    .FirstOrDefaultAsync();

                if (entity == null) return new ResponseDTO("Bài đăng không tồn tại.", 404, false);

                // BƯỚC 2: Check Cache
                if (!string.IsNullOrEmpty(entity.AIAnalysisJson))
                {
                    try
                    {
                        var cachedResult = JsonSerializer.Deserialize<AIAnalysisResult>(entity.AIAnalysisJson);
                        if (cachedResult != null)
                            return new ResponseDTO("Lấy kết quả từ Cache.", 200, true, cachedResult);
                    }
                    catch { /* Lỗi JSON thì bỏ qua */ }
                }

                // BƯỚC 3: Map Entity -> DTO (Object) để gửi AI
                var dataForAI = MapEntityToDataForAI_Trip(entity);

                // BƯỚC 4: Gọi AI phân tích
                var aiResponse = await _aiService.AnalyzePostTripAsync(dataForAI);

                if (!aiResponse.IsSuccess)
                    return new ResponseDTO("Lỗi AI: " + aiResponse.RawContent, 500, false);

                // BƯỚC 5: Lưu Cache
                entity.AIAnalysisJson = JsonSerializer.Serialize(aiResponse.Result);
                await _unitOfWork.PostTripRepo.UpdateAsync(entity);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Phân tích AI thành công.", 200, true, aiResponse.Result);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi: {ex.Message}", 500, false);
            }
        }

        // Helper mapping riêng cho PostTrip (để gửi AI)
        private object MapEntityToDataForAI_Trip(PostTrip p)
        {
            return new PostTripViewDTO
            {
                PostTripId = p.PostTripId,
                Title = p.Title,
                Description = p.Description,
                Status = p.Status,
                CreateAt = p.CreateAt,
                RequiredPayloadInKg = p.RequiredPayloadInKg,
                Owner = p.Owner == null ? null : new OwnerSimpleDTO
                {
                    UserId = p.Owner.UserId,
                    FullName = p.Owner.FullName,
                    CompanyName = p.Owner.CompanyName
                },
                Trip = p.Trip == null ? null : new TripSummaryForPostDTO
                {
                    TripId = p.Trip.TripId,
                    StartLocationName = p.Trip.ShippingRoute?.StartLocation?.Address ?? string.Empty,
                    EndLocationName = p.Trip.ShippingRoute?.EndLocation?.Address ?? string.Empty,
                    StartTime = p.Trip.ShippingRoute?.PickupTimeWindow?.StartTime ?? default(TimeOnly),
                    VehicleModel = p.Trip.Vehicle?.Model,
                    VehiclePlate = p.Trip.Vehicle?.PlateNumber,
                    VehicleType = p.Trip.Vehicle?.VehicleType?.VehicleTypeName,
                    PackageCount = p.Trip.Packages?.Count ?? 0
                },
                PostTripDetails = p.PostTripDetails.Select(d => new PostTripDetailViewDTO
                {
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
    }
}