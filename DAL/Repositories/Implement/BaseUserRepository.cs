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
    public class BaseUserRepository : GenericRepository<BaseUser>, IBaseUserRepository
    {
        private readonly DriverShareAppContext _context;
        public BaseUserRepository (DriverShareAppContext context) : base (context)
        {
            _context = context;
        }
        public async Task<BaseUser> FindByEmailAsync(string email)
        {
            return await _context.BaseUsers
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == email);
        }
    }
}
