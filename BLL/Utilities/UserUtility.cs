using Common.Constants;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Security.Claims;

namespace BLL.Utilities
{
    public class UserUtility
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserUtility(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        // --- METHOD 1: Lấy UserID ---

        // Cách dùng cũ (Cho Controller): Tự lấy từ HttpContext
        public Guid GetUserIdFromToken()
        {
            return GetUserId(_httpContextAccessor.HttpContext?.User);
        }

        // Cách dùng mới (Cho SignalR): Truyền Context.User vào
        public Guid GetUserId(ClaimsPrincipal? user)
        {
            if (user == null) return Guid.Empty;

            var userIdClaim = user.Claims.FirstOrDefault(c =>
                c.Type == JwtConstant.KeyClaim.userId ||
                c.Type == ClaimTypes.NameIdentifier ||
                c.Type == "sub");

            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out Guid userId))
            {
                return userId;
            }
            return Guid.Empty;
        }

        // --- METHOD 2: Lấy Role ---

        // Cách dùng cũ (Cho Controller)
        public string? GetUserRoleFromToken()
        {
            return GetUserRole(_httpContextAccessor.HttpContext?.User);
        }

        // Cách dùng mới (Cho SignalR)
        public string? GetUserRole(ClaimsPrincipal? user)
        {
            if (user == null) return null;

            var roleClaim = user.Claims.FirstOrDefault(c =>
                c.Type == JwtConstant.KeyClaim.Role ||
                c.Type == ClaimTypes.Role);

            return roleClaim?.Value;
        }

        // --- METHOD 3: Lấy Name (Tiện ích thêm cho Hub) ---
        public string GetUserName(ClaimsPrincipal? user, string defaultName = "Tài xế")
        {
            if (user == null) return defaultName;
            return user.FindFirst(ClaimTypes.Name)?.Value
                ?? user.Identity?.Name
                ?? defaultName;
        }
    }
}