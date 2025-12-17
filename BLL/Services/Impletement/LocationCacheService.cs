using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class LocationCacheService
    {
        // Key: TripId (Guid), Value: Object chứa tọa độ
        private readonly ConcurrentDictionary<Guid, object> _lastLocations = new();

        // Lưu hoặc cập nhật vị trí mới nhất
        public void UpdateLocation(Guid tripId, object data)
        {
            _lastLocations.AddOrUpdate(tripId, data, (k, v) => data);
        }

        // Lấy vị trí (để gửi cho người mới vào xem)
        public object? GetLocation(Guid tripId)
        {
            _lastLocations.TryGetValue(tripId, out var data);
            return data;
        }

        // Xóa khi kết thúc chuyến (quan trọng để không đầy RAM)
        public void RemoveLocation(Guid tripId)
        {
            _lastLocations.TryRemove(tripId, out _);
        }
    }
}
