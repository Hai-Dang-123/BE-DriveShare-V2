using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.ValueObjects
{
    public class Location // Có thể là struct hoặc class tùy ý định
    {
        public string? Address { get; private set; } = null!;
        public double? Latitude { get; private set; }
        public double? Longitude { get; private set; }

        public Location() { }

        // Constructor để đảm bảo tính bất biến và khởi tạo đầy đủ
        public Location(string address, double latitude, double longitude)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("Address cannot be null or empty.", nameof(address));
            // Có thể thêm validation cho latitude/longitude
            Address = address;
            Latitude = latitude;
            Longitude = longitude;
        }

        // Nên override Equals và GetHashCode cho Value Object
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
