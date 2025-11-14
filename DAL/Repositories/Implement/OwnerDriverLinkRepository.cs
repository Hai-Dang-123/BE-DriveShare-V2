using Common.Enums.Status;
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
    public class OwnerDriverLinkRepository : GenericRepository<OwnerDriverLink>, IOwnerDriverLinkRepository
    {
        private readonly DriverShareAppContext _context;
        public OwnerDriverLinkRepository (DriverShareAppContext context): base(context)
        {
            _context = context;
        }

        public async Task<bool> CheckLinkExistsAsync(Guid ownerId, Guid driverId)
        {
            return await _context.OwnerDriverLinks
                .AnyAsync(link =>
                    link.OwnerId == ownerId &&
                    link.DriverId == driverId &&
                    (link.Status == FleetJoinStatus.PENDING || link.Status == FleetJoinStatus.APPROVED));
        }

        public async Task<bool> CheckLinkExistsAsync(Guid ownerId, Guid driverId, FleetJoinStatus? status = null)
        {
            var query = _context.OwnerDriverLinks.Where(l => l.OwnerId == ownerId && l.DriverId == driverId);

            // Nếu status có giá trị (vd: APPROVED), lọc thêm theo status
            if (status.HasValue)
            {
                query = query.Where(l => l.Status == status.Value);
            }

            // Trả về true nếu tìm thấy bất kỳ record nào khớp
            return await query.AnyAsync();
        }

    }
}
