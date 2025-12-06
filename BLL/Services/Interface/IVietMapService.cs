using BLL.Services.Impletement;
using Common.DTOs;
using Common.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IVietMapService
    {
        /// <summary>
        /// Gọi API VietMap Search v3, trả về danh sách các địa điểm
        /// </summary>
        /// <param name="text">Địa chỉ cần tìm</param>
        /// <param name="focusLatLon">Chuỗi "latitude,longitude" để ưu tiên kết quả</param>
        /// <returns>Danh sách kết quả VietMapGeocodeResult</returns>
        Task<List<VietMapGeocodeResultV2>> SearchAsync(string text, string? focusLatLon = null);

        /// <summary>
        // Hàm helper để tìm và trả về tọa độ của địa chỉ
        /// </summary>
        /// <param name="address">Địa chỉ dạng chuỗi (ví dụ: "197 Trần Phú, Q5, HCM")</param>
        /// <param name="focusLatLon">Chuỗi "latitude,longitude" để ưu tiên kết quả</param>
        /// <returns>Một đối tượng Location (với lat/lon) hoặc null nếu không tìm thấy</returns>
        Task<Location?> GeocodeAsync(string address, string? focusLatLon = null);

        /// <summary>
        /// Lấy thông tin tuyến đường tối ưu (dùng API Route v3)
        /// </summary>
        /// <param name="start">Tọa độ điểm đi</param>
        /// <param name="end">Tọa độ điểm đến</param>
        /// <param name="vehicleType">Loại xe ("car", "motorcycle", "truck")</param>
        /// <param name="capacityKg">Trọng tải (kg), BẮT BUỘC nếu vehicleType="truck"</param>
        /// <returns>Đối tượng VietMapPath chứa (polyline, distance, time) hoặc null nếu lỗi</returns>
        Task<VietMapPath?> GetRouteAsync(Location start, Location end, string vehicleType = "car", int capacityKg = 0);


        /// <summary>
        /// hàm để ước tính thời gian di chuyển (giờ) giữa 2 điểm
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="vehicle"></param>
        /// <returns></returns>
        Task<double> GetEstimatedDurationHoursAsync(Location start, Location end, string vehicle = "truck");

        /// <summary>
        /// HÀM KIỂM TRA ĐIỂM CÓ NẰM TRÊN TUYẾN ĐƯỜNG HAY KHÔNG
        /// </summary>
        /// <param name="point"></param>
        /// <param name="encodedPolyline"></param>
        /// <param name="bufferKm"></param>
        /// <returns></returns>
        bool IsLocationOnRoute(Location point, string encodedPolyline, double bufferKm = 5.0);
    }
}
