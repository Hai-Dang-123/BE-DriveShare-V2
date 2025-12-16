using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System;

namespace BLL.Hubs
{
    public class TripTrackingHub : Hub
    {
        // SỬA: Key của Dictionary đổi thành Guid cho đúng kiểu dữ liệu
        // Lưu trữ vị trí cuối cùng: Key = TripId (Guid), Value = Object chứa tọa độ
        private static readonly ConcurrentDictionary<Guid, object> _lastLocations = new();

        // ==========================================================
        // 1. NGƯỜI XEM (Viewer) GỌI HÀM NÀY
        // ==========================================================
        public async Task JoinTripGroup(Guid tripId)
        {
            // 1. Convert Guid sang string để đặt tên nhóm (SignalR Group name bắt buộc là string)
            string groupName = GetGroupName(tripId);

            // 2. Add connection vào nhóm
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            // 3. LOGIC BACKEND: Kiểm tra xem có "ảnh chụp" (Cache) nào của chuyến này chưa?
            // Nếu có -> Gửi NGAY LẬP TỨC cho người vừa vào (Caller)
            if (_lastLocations.TryGetValue(tripId, out var lastLocation))
            {
                // Chỉ gửi cho chính người vừa gọi hàm này (Caller), không spam cả nhóm
                await Clients.Caller.SendAsync("ReceiveLocation", lastLocation);
            }
        }

        // ==========================================================
        // 2. NGƯỜI XEM RỜI KHỎI MÀN HÌNH
        // ==========================================================
        public async Task LeaveTripGroup(Guid tripId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(tripId));
        }

        // ==========================================================
        // 3. TÀI XẾ (Driver) GỌI HÀM NÀY ĐỂ GỬI VỊ TRÍ
        // ==========================================================
        public async Task SendLocationUpdate(Guid tripId, double lat, double lng, double bearing, double speed)
        {
            // Lấy tên tài xế từ Token (đã xác thực)
            var driverName = Context.User?.Identity?.Name ?? "Tài xế";

            // Tạo object dữ liệu
            var locationData = new
            {
                Lat = lat,
                Lng = lng,
                Bearing = bearing,
                Speed = speed,
                DriverName = driverName,
                UpdatedAt = DateTime.UtcNow
            };

            // 1. LOGIC BACKEND: Lưu đè vị trí này vào Cache (Dictionary)
            // Để dành cho người nào vào sau thì có cái xem ngay
            _lastLocations.AddOrUpdate(tripId, locationData, (key, oldValue) => locationData);

            // 2. Gửi cho tất cả mọi người đang xem (trừ ông tài xế đang gửi)
            string groupName = GetGroupName(tripId);
            await Clients.GroupExcept(groupName, Context.ConnectionId).SendAsync("ReceiveLocation", locationData);
        }

        // Hàm phụ trợ để thống nhất cách đặt tên Group
        private string GetGroupName(Guid tripId)
        {
            return $"TRIP_{tripId.ToString().ToUpper()}";
        }
    }
}