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

        public async Task<BaseUser?> FindByEmailAndRoleAsync(string email, string roleName)
        {
            return await _context.BaseUsers
                .Include(u => u.Role)
                .Where(u => u.Email == email && u.Role.RoleName == roleName)
                .FirstOrDefaultAsync();
        }

        public async Task<BaseUser> FindByPhoneNumberAsync(string phoneNumber)
        {
            return await _context.BaseUsers
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
        }

        public async Task<BaseUser?> GetBaseUserByIdAsync(Guid userId)
        {
            // Phương thức dự phòng cho các vai trò khác (ví dụ: Admin)
            return await _context.BaseUsers
                .AsNoTracking()
                .Include(u => u.Role) // Cần để lấy tên Role
                .FirstOrDefaultAsync(u => u.UserId == userId);
        }
    }
}
