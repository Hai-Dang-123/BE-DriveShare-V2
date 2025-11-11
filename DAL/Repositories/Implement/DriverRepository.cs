using DAL.Context;
using DAL.Entities;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Repositories.Implement
{
    public class DriverRepository : GenericRepository<Driver>, IDriverRepository
    {
        private readonly DriverShareAppContext _context;
        public DriverRepository (DriverShareAppContext context) : base(context)
        {
            _context = context;
        }

        public async Task<Driver?> GetDriverProfileAsync(Guid userId)
        {
            return await _context.Drivers
                .OfType<Driver>() // Lọc chỉ lấy Driver
                .AsNoTracking()
                .Include(d => d.TripDriverAssignments) // Cần cho "analysis"
                .Include(d => d.OwnerDriverLinks)      // Cần cho "analysis"
                .FirstOrDefaultAsync(d => d.UserId == userId);
        }
    }
}
