using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BLL.Hubs
{
    public class TripTrackingHub : Hub
    {
        // 1. Join vào nhóm theo TripId (Dành cho Owner và Tài xế khi mở màn hình Map)
        public async Task JoinTripGroup(string tripId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"TRIP_{tripId}");
        }

        // 2. Rời nhóm
        public async Task LeaveTripGroup(string tripId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"TRIP_{tripId}");
        }

        // 3. Gửi cập nhật vị trí (App Driver gọi hàm này liên tục)
        public async Task SendLocationUpdate(string tripId, double lat, double lng, double bearing, double speed, string driverName, string role)
        {
            // Gửi cho tất cả trong nhóm (trừ người gửi để tiết kiệm băng thông)
            await Clients.GroupExcept($"TRIP_{tripId}", Context.ConnectionId).SendAsync("ReceiveLocation", new
            {
                Lat = lat,
                Lng = lng,
                Bearing = bearing,
                Speed = speed,
                DriverName = driverName,
                DriverRole = role,
                UpdatedAt = DateTime.UtcNow
            });
        }
    }
}
