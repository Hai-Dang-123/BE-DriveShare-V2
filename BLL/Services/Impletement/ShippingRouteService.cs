using BLL.Services.Interface;
using Common.DTOs;
using Common.ValueObjects;
using DAL.Entities;
using DAL.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class ShippingRouteService : IShippingRouteService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IVietMapService _vietMapService;
        public ShippingRouteService(IUnitOfWork unitOfWork, IVietMapService vietMapService)
        {
            _unitOfWork = unitOfWork;
            _vietMapService = vietMapService;
        }

        public async Task<ResponseDTO> CreateShippingRouteAsync(CreateShippingRouteDTO dto)
        {
            // 1. Geocode điểm đi
            Location startLocation = await _vietMapService.GeocodeAsync(dto.StartLocation);
            if (startLocation == null || startLocation.Latitude == 0) // Kiểm tra kỹ hơn
            {
                return new ResponseDTO("Unable to geocode start location.", 400, false);
            }

            // 2. Geocode điểm đến
            Location endLocation = await _vietMapService.GeocodeAsync(dto.EndLocation);
            if (endLocation == null || endLocation.Latitude == 0) // Kiểm tra kỹ hơn
            {
                return new ResponseDTO("Unable to geocode end location.", 400, false);
            }

            // 3. Tạo đối tượng
            var newShippingRoute = new ShippingRoute
            {
                ShippingRouteId = Guid.NewGuid(),
                StartLocation = startLocation,
                EndLocation = endLocation,
                ExpectedPickupDate = dto.ExpectedPickupDate,
                ExpectedDeliveryDate = dto.ExpectedDeliveryDate,
                PickupTimeWindow = new TimeWindow(dto.StartTimeToPickup, dto.EndTimeToPickup),
                DeliveryTimeWindow = new TimeWindow(dto.StartTimeToDelivery, dto.EndTimeToDelivery),
            };

            // 4. Lưu vào Database (Phần bị thiếu)
            try
            {
                await _unitOfWork.ShippingRouteRepo.AddAsync(newShippingRoute);
                await _unitOfWork.SaveChangeAsync();

                // 5. Trả về thành công (Phần bị thiếu)
                return new ResponseDTO("Shipping Route created successfully.", 201, true, new
                {
                    shippingRouteId = newShippingRoute.ShippingRouteId,
                });
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error saving Shipping Route: {ex.Message}", 500, false);
            }
        }
    }
}
