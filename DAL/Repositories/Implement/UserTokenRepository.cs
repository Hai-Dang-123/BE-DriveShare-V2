using Common.Enums.Type;
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
    public class UserTokenRepository : GenericRepository<UserToken>, IUserTokenRepository
    {
        private readonly DriverShareAppContext _context;
        public UserTokenRepository (DriverShareAppContext context) : base (context)
        {
            _context = context;
        }

        public async Task<UserToken> GetRefreshTokenByUserID(Guid userId)
        {
            // lấy token đúng id và chưa bị thu hồi
            return await _context.UserTokens
                .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                .FirstOrDefaultAsync();
        }


        public async Task<UserToken?> GetValidRefreshTokenWithUserAsync(string tokenValue)
        {
            if (string.IsNullOrWhiteSpace(tokenValue))
            {
                return null;
            }

            // Dùng _dbSet từ GenericRepository để truy vấn
            return await _context.UserTokens
                .Include(rt => rt.User) // Include User
                    .ThenInclude(u => u.Role) // Từ User, Include tiếp Role
                .FirstOrDefaultAsync(rt => rt.TokenValue == tokenValue
                                         && rt.TokenType == TokenType.REFRESH);
            // Bạn có thể thêm điều kiện !rt.IsRevoked ở đây nếu muốn
            // hoặc xử lý kiểm tra IsRevoked ở Service
        }
    }
}
