using DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Repositories.Interface
{
    public interface IVehicleRepository : IGenericRepository<Vehicle>
    {
        Task<List<Vehicle>> GetAvailableVehiclesAsync();
        Task<List<Vehicle>> GetVehiclesByOwnerIdAsync(Guid ownerId);
    }
}
