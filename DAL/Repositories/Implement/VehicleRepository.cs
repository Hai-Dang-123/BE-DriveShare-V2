using DAL.Context;
using DAL.Entities;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DAL.Repositories.Implement
{
    public class VehicleRepository : GenericRepository<Vehicle>, IVehicleRepository
    {
        private readonly DriverShareAppContext _context;

        public VehicleRepository(DriverShareAppContext context) : base(context)
        {
            _context = context;
        }

        // Lấy danh sách xe đang ACTIVE
        public async Task<List<Vehicle>> GetAvailableVehiclesAsync()
        {
            return await _context.Vehicles
                .Include(v => v.VehicleType)
                .Include(v => v.Owner)
                .Where(v => v.Status == Common.Enums.Status.VehicleStatus.ACTIVE)
                .ToListAsync();
        }

        // Lấy tất cả xe của 1 Owner
        public async Task<List<Vehicle>> GetVehiclesByOwnerIdAsync(Guid ownerId)
        {
            return await _context.Vehicles
                .Include(v => v.VehicleImages)
                .Include(v => v.VehicleDocuments)
                .Where(v => v.OwnerId == ownerId && v.Status != Common.Enums.Status.VehicleStatus.DELETED)
                .ToListAsync();
        }

        public IQueryable<Vehicle> GetVehiclesByOwnerIdQueryable(Guid ownerId)
        {
            // Trả về IQueryable, không .ToListAsync()
            return _context.Vehicles
                .AsNoTracking()
                .Where(v => v.OwnerId == ownerId)
                // Include các bảng liên quan để map sang DTO
                .Include(v => v.Owner)
                .Include(v => v.VehicleType)
                .Include(v => v.VehicleImages)
                // (Không include VehicleDocuments ở đây để giữ cho list nhẹ)
                .OrderByDescending(v => v.CreatedAt); // Luôn OrderBy khi paging
        }
    }
}
