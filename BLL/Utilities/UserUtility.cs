using Common.Constants;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Utilities
{
    public class UserUtility
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserUtility(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }
        //public Guid GetUserIdFromToken()
        //{
        //    var userIdClaim = _httpContextAccessor.HttpContext?.User.Claims.FirstOrDefault(c => c.Type == "userId" || c.Type == "sub");
        //    if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out Guid userId))
        //    {
        //        return userId;
        //    }
        //    return Guid.Empty;
        //}

        public Guid GetUserIdFromToken()
        {
            // Cập nhật để dùng hằng số (nhất quán với LoginAsync)
            var userIdClaim = _httpContextAccessor.HttpContext?.User.Claims.FirstOrDefault(c =>
                c.Type == JwtConstant.KeyClaim.userId ||
                c.Type == ClaimTypes.NameIdentifier || // Fallback tiêu chuẩn
                c.Type == "sub");

            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out Guid userId))
            {
                return userId;
            }
            return Guid.Empty;
        }

        public string? GetUserRoleFromToken()
        {
            // Đọc claim Role dựa trên hằng số bạn đã set khi Login
            var roleClaim = _httpContextAccessor.HttpContext?.User.Claims.FirstOrDefault(c =>
                c.Type == JwtConstant.KeyClaim.Role ||
                c.Type == ClaimTypes.Role); // Fallback tiêu chuẩn

            if (roleClaim != null)
            {
                return roleClaim.Value;
            }
            return null; // Không tìm thấy Role
        }
    }
}
