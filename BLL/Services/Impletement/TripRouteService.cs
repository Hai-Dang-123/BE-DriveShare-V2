using BLL.Services.Interface;
using Common.DTOs;
using DAL.Entities;
using DAL.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BLL.Utilities;

namespace BLL.Services.Impletement
{
    public class TripRouteService : ITripRouteService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IVietMapService _vietMapService;

        public TripRouteService(IUnitOfWork unitOfWork, IVietMapService vietMapService)
        {
            _unitOfWork = unitOfWork;
            _vietMapService = vietMapService;
        }

        //public async Task<ResponseDTO> GenerateTripRouteAsync(Guid tripId)
        //{
        //    // 1. Lấy dữ liệu Trip và các bảng liên quan (ShippingRoute, Vehicle, VehicleType)
        //    var trip = await _unitOfWork.TripRepo.FirstOrDefaultAsync(
        //        t => t.TripId == tripId,
        //        includeProperties: "ShippingRoute,Vehicle.VehicleType" // Include sâu
        //    );

        //    // 2. Kiểm tra dữ liệu đầu vào
        //    if (trip == null)
        //    {
        //        return new ResponseDTO("Trip not found.", 404, false);
        //    }
           
        //    if (trip.Vehicle == null || trip.Vehicle.VehicleType == null)
        //    {
        //        return new ResponseDTO("Trip does not have an assigned Vehicle or VehicleType.", 400, false);
        //    }
           

        //    // 2.1 Kiểm tra xem TripRoute đã được tạo chưa
        //    if (trip.TripRouteId != null && trip.TripRouteId != Guid.Empty)
        //    {
        //        // Bạn có thể chọn cập nhật (Update) hoặc báo lỗi (Conflict)
        //        // Ở đây tôi chọn báo lỗi để tránh tạo trùng
        //        return new ResponseDTO("This Trip already has a generated route.", 409, false); // 409 Conflict
        //    }

            
        //    // Map VehicleType của bạn sang API của VietMap
        //    string vehicleTypeApi = "car"; // Mặc định
        //    int capacityKg = 0;

        //    string vehicleTypeNameLower = trip.Vehicle.VehicleType.VehicleTypeName.ToLower();

        //    if (vehicleTypeNameLower.Contains("tải") || vehicleTypeNameLower.Contains("truck"))
        //    {
        //        vehicleTypeApi = "truck";
        //        capacityKg = (int)trip.Vehicle.PayloadInKg;
        //    }
        //    else if (vehicleTypeNameLower.Contains("máy") || vehicleTypeNameLower.Contains("motorcycle"))
        //    {
        //        vehicleTypeApi = "motorcycle";
        //    }

        //    // 4. Gọi VietMapService để lấy tuyến đường
        //    var path = await _vietMapService.GetRouteAsync(start, end, vehicleTypeApi, capacityKg);

        //    if (path == null)
        //    {
        //        return new ResponseDTO("Failed to find a valid route from VietMap API.", 503, false); // 503 Service Unavailable
        //    }

        //    // 5. Tạo đối tượng TripRoute mới
        //    var newTripRoute = new TripRoute
        //    {
        //        TripRouteId = Guid.NewGuid(),
        //        RouteData = path.Points, // Chuỗi polyline mã hóa
        //        DistanceKm = (decimal)path.Distance / 1000m, // Chuyển mét -> km
        //        Duration = TimeSpan.FromMilliseconds(path.Time), // Chuyển mili giây -> TimeSpan
        //        CreateAt = DateTime.UtcNow,
        //        UpdateAt = DateTime.UtcNow
        //        // TripId sẽ được gán qua Navigation Property hoặc Khóa ngoại
        //    };

        //    // 6. Lưu vào Database (Sử dụng Transaction)
        //    await _unitOfWork.BeginTransactionAsync();
        //    try
        //    {
        //        // Thêm TripRoute mới
        //        await _unitOfWork.TripRouteRepo.AddAsync(newTripRoute);

        //        // Cập nhật Trip để trỏ đến TripRouteId mới
        //        trip.TripRouteId = newTripRoute.TripRouteId;
        //        await _unitOfWork.TripRepo.UpdateAsync(trip); // Dùng Update (non-async)

        //        // Lưu tất cả thay đổi
        //        await _unitOfWork.CommitTransactionAsync();

        //        return new ResponseDTO("TripRoute created successfully.", 201, true, new
        //        {
        //            tripRouteId = newTripRoute.TripRouteId,
        //            distanceKm = newTripRoute.DistanceKm,
        //            durationMinutes = newTripRoute.Duration.TotalMinutes
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        await _unitOfWork.RollbackTransactionAsync();
        //        // TODO: Log lỗi chi tiết (ex.Message)
        //        return new ResponseDTO($"Failed to save TripRoute: {ex.Message}", 500, false);
        //    }
        //}

        public async Task<ResponseDTO> GetTripRouteByIdAsync(Guid tripRouteId)
        {
            var route = await _unitOfWork.TripRouteRepo.GetByIdAsync(tripRouteId);
            if (route == null)
            {
                return new ResponseDTO("TripRoute not found.", 404, false);
            }
            return new ResponseDTO("Success", 200, true, route);
        }

        public async Task<ResponseDTO> GetRouteForTripAsync(Guid tripId)
        {
            var trip = await _unitOfWork.TripRepo.GetByIdAsync(tripId);
            if (trip == null)
            {
                return new ResponseDTO("Trip not found.", 404, false);
            }

            if (trip.TripRouteId == null || trip.TripRouteId == Guid.Empty)
            {
                return new ResponseDTO("This Trip does not have a generated route yet.", 404, false);
            }

            return await GetTripRouteByIdAsync(trip.TripRouteId);
        }

        public async Task<TripRoute> CreateAndAddTripRouteAsync(ShippingRoute shippingRoute, Vehicle vehicle)
        {
            // 1. Kiểm tra dữ liệu đầu vào
            if (shippingRoute == null)
            {
                throw new ArgumentNullException(nameof(shippingRoute), "ShippingRoute (plan) cannot be null.");
            }
            if (vehicle == null || vehicle.VehicleType == null)
            {
                throw new ArgumentNullException(nameof(vehicle), "Vehicle or VehicleType cannot be null.");
            }
            if (shippingRoute.StartLocation.Latitude == 0 || shippingRoute.EndLocation.Latitude == 0)
            {
                throw new InvalidOperationException("ShippingRoute Start/End locations have not been geocoded.");
            }

            // 2. Chuẩn bị tham số cho API Route
            var start = shippingRoute.StartLocation;
            var end = shippingRoute.EndLocation;

            // Map VehicleType
            string vehicleTypeApi = "truck";
            int capacityKg = 0;
            string vehicleTypeNameLower = vehicle.VehicleType.VehicleTypeName.ToLower();

            //if (vehicleTypeNameLower.Contains("tải") || vehicleTypeNameLower.Contains("truck"))
            //{
            //    vehicleTypeApi = "truck";
            //    capacityKg = (int)vehicle.PayloadInKg;
            //}
            //else if (vehicleTypeNameLower.Contains("máy") || vehicleTypeNameLower.Contains("motorcycle"))
            //{
            //    vehicleTypeApi = "motorcycle";
            //}

            // 3. Gọi VietMapService
            var path = await _vietMapService.GetRouteAsync(start, end, vehicleTypeApi, capacityKg);

            if (path == null)
            {
                // Ném lỗi để Transaction bên ngoài (trong TripService) có thể Rollback
                throw new Exception("Failed to find a valid route from VietMap API.");
            }

            // 4. Tạo đối tượng TripRoute mới
            var newTripRoute = new TripRoute
            {
                TripRouteId = Guid.NewGuid(),
                RouteData = path.Points,
                DistanceKm = (decimal)path.Distance / 1000m,
                Duration = TimeSpan.FromMilliseconds(path.Time),
                CreateAt = TimeUtil.NowVN(),
                UpdateAt = TimeUtil.NowVN()
            };

            // 5. Thêm vào UoW (KHÔNG SAVE)
            await _unitOfWork.TripRouteRepo.AddAsync(newTripRoute);

            // 6. Trả về entity để TripService sử dụng
            return newTripRoute;
        }
    }

}
