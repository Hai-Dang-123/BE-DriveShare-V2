using Common.DTOs;
using DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface ITripRouteService
    {
        /// <summary>
        /// Tạo (generate) tuyến đường chi tiết cho một Trip đã có.
        /// Sẽ đọc ShippingRouteId và VehicleId từ Trip,
        /// gọi VietMap API Route v3, sau đó tạo và lưu TripRoute.
        /// </summary>
        /// <param name="tripId">ID của Trip cần tạo tuyến đường</param>
        /// <returns>ResponseDTO chứa TripRouteId mới</returns>
        Task<ResponseDTO> GenerateTripRouteAsync(Guid tripId);

        /// <summary>
        /// Lấy thông tin TripRoute (đã được tạo) bằng ID của nó.
        /// </summary>
        /// <param name="tripRouteId">ID của TripRoute</param>
        /// <returns>ResponseDTO chứa thông tin TripRoute</returns>
        Task<ResponseDTO> GetTripRouteByIdAsync(Guid tripRouteId);

        /// <summary>
        /// Lấy thông tin TripRoute (đã được tạo) bằng TripId.
        /// </summary>
        /// <param name="tripId">ID của Trip</param>
        /// <returns>ResponseDTO chứa thông tin TripRoute</returns>
        Task<ResponseDTO> GetRouteForTripAsync(Guid tripId);

        Task<TripRoute> CreateAndAddTripRouteAsync(ShippingRoute shippingRoute, Vehicle vehicle);


    }
}
