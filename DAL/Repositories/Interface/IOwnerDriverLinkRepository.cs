using Common.Enums.Status;
using DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Repositories.Interface
{
    public interface IOwnerDriverLinkRepository : IGenericRepository<OwnerDriverLink>
    {
        Task<bool> CheckLinkExistsAsync(Guid ownerId, Guid driverId);
        Task<bool> CheckLinkExistsAsync(Guid ownerId, Guid driverId, FleetJoinStatus? status = null);
    }
}
