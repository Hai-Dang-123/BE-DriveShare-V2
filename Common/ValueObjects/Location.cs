using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.ValueObjects
{
    public class Location
    {
        // SỬA Ở ĐÂY: Đổi 'private set' thành 'set' để API nhận được dữ liệu
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public Location() { }

        public Location(string address, double latitude, double longitude)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("Address cannot be null or empty.", nameof(address));

            Address = address;
            Latitude = latitude;
            Longitude = longitude;
        }

        // Giữ nguyên phần override Equals và GetHashCode...
        public override bool Equals(object? obj)
        {
            return obj is Location other &&
                   Address == other.Address &&
                   Latitude == other.Latitude &&
                   Longitude == other.Longitude;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Address, Latitude, Longitude);
        }
    }
}
