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

    }
}
