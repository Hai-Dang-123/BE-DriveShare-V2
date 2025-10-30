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
    public class RoleRepository : GenericRepository<Role>, IRoleRepository
    {
        private readonly DriverShareAppContext _context;
        public RoleRepository (DriverShareAppContext context) : base (context)
        {
            _context = context;
        }

        public async Task<Role?> GetByName(string name)
        {
            return await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == name);
        }
    }
}
