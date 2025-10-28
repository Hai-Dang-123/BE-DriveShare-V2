﻿using Common.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class ShippingRoute
    {
        public Guid ShippingRouteId { get; set; }

        // --- Chi tiết Lộ trình ---
        public Location StartLocation { get; set; } = null!;
        public Location EndLocation { get; set; } = null!;
        public DateTime ExpectedPickupDate { get; set; }
        public DateTime ExpectedDeliveryDate { get; set; }
        public TimeWindow PickupTimeWindow { get; set; } = new(null, null);
        public TimeWindow DeliveryTimeWindow { get; set; } = new(null, null);
        public decimal EstimatedDistanceKm { get; set; }

        // --- GỢI Ý BẮT BUỘC (Navigation Properties - Dựa trên quan hệ 1-1) ---

        // Lộ trình này được sử dụng bởi Chuyến đi nào?
        public virtual Trip? Trip { get; set; }

        // Lộ trình này được sử dụng bởi Bài đăng gói cước nào?
        public virtual PostPackage? PostPackage { get; set; }


    }
}