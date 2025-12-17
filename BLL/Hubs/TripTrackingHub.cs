using BLL.Services.Impletement; // Namespace chứa LocationCacheService
using DAL.UnitOfWork;
using DAL.Entities;
using Common.Enums.Status;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Hubs
{
    [Authorize] // Bắt buộc phải có Token (đăng nhập) mới kết nối được
    public class TripTrackingHub : Hub
    {
        private readonly LocationCacheService _cacheService;
        private readonly IUnitOfWork _unitOfWork;

        public TripTrackingHub(LocationCacheService cacheService, IUnitOfWork unitOfWork)
        {
            _cacheService = cacheService;
            _unitOfWork = unitOfWork;
        }

        // =========================================================================
        // 1. DÀNH CHO DRIVER (TÀI XẾ): GỬI VỊ TRÍ
        // =========================================================================
        public async Task SendLocationUpdate(Guid tripId, double lat, double lng, double bearing, double speed)
        {
            // Lấy tên tài xế từ Token để hiển thị cho người xem
            // Context.User.Identity.Name thường được map từ Claim "sub" hoặc "id" trong JWT
            var driverName = Context.User?.Identity?.Name ?? "Tài xế";

            // Nếu muốn lấy tên thật, có thể query DB hoặc lấy từ Claim "name" nếu có
            var realName = Context.User?.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? driverName;

            var data = new
            {
                Lat = lat,
                Lng = lng,
                Bearing = bearing,
                Speed = speed,
                DriverName = realName,
                UpdatedAt = DateTime.UtcNow
            };

            // 1. Lưu vào Cache RAM (Service Singleton)
            // Để người sau vào xem lấy được ngay
            _cacheService.UpdateLocation(tripId, data);

            // 2. Gửi cho tất cả người đang xem (trừ chính tài xế gửi)
            // Group name quy ước: TRIP_{Guid}
            await Clients.GroupExcept($"TRIP_{tripId}", Context.ConnectionId)
                         .SendAsync("ReceiveLocation", data);
        }

        // =========================================================================
        // 2. DÀNH CHO VIEWER (OWNER, PROVIDER, STAFF): THAM GIA XEM
        // =========================================================================
        public async Task JoinTripGroup(Guid tripId)
        {
            // A. Lấy ID người dùng từ Token
            if (Context.User?.Identity?.Name == null ||
                !Guid.TryParse(Context.User.Identity.Name, out Guid currentUserId))
            {
                throw new HubException("Unauthorized: Invalid Token");
            }

            // B. CHECK QUYỀN (Authorization Logic)
            // Phải kiểm tra xem người này có liên quan gì đến Trip không?
            bool isAllowed = await CheckPermissionAsync(currentUserId, tripId);

            if (!isAllowed)
            {
                throw new HubException("Forbidden: Bạn không có quyền theo dõi chuyến đi này.");
            }

            // C. Nếu có quyền -> Add vào Group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"TRIP_{tripId}");

            // D. Gửi ngay vị trí cuối cùng (Last Known Location) từ Cache
            // Giúp Admin không phải chờ 5s mới thấy xe xuất hiện
            var lastLoc = _cacheService.GetLocation(tripId);
            if (lastLoc != null)
            {
                await Clients.Caller.SendAsync("ReceiveLocation", lastLoc);
            }
        }

        public async Task LeaveTripGroup(Guid tripId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"TRIP_{tripId}");
        }

        // =========================================================================
        // 3. PRIVATE HELPER: KIỂM TRA QUYỀN TRUY CẬP DB
        // =========================================================================
        private async Task<bool> CheckPermissionAsync(Guid userId, Guid tripId)
        {
            // 1. Check Role Admin/Staff (Quyền lực tối cao)
            var role = Context.User?.Claims.FirstOrDefault(c => c.Type == "role")?.Value;
            // Tùy vào cách bạn đặt tên Role trong hệ thống
            if (!string.IsNullOrEmpty(role) && (role.ToUpper() == "ADMIN" || role.ToUpper() == "STAFF"))
            {
                return true;
            }

            // 2. Lấy thông tin Trip kèm các quan hệ
            // Dùng AsNoTracking để tối ưu hiệu năng vì chỉ đọc
            var trip = await _unitOfWork.TripRepo.GetAll()
                .AsNoTracking()
                .Include(t => t.TripProviderContract) // Include để check Provider
                .Include(t => t.DriverAssignments)    // Include để check Driver
                .FirstOrDefaultAsync(t => t.TripId == tripId);

            if (trip == null) return false;

            // 3. Check Owner (Chủ hàng)
            if (trip.OwnerId == userId) return true;

            // 4. Check Provider (Nhà cung cấp vận tải)
            // Logic: Nếu Trip có hợp đồng với Provider, và User hiện tại là Provider đó
            if (trip.TripProviderContract != null && trip.TripProviderContract.CounterpartyId == userId)
            {
                return true;
            }

            // 5. Check Driver (Tài xế lái chuyến này)
            // Logic: Có phân công và trạng thái là đã chấp nhận
            var isAssignedDriver = trip.DriverAssignments
                .Any(a => a.DriverId == userId &&
                          a.AssignmentStatus == AssignmentStatus.ACCEPTED); // Hoặc AssignmentStatus.ASSIGNED tùy quy ước

            if (isAssignedDriver) return true;

            // Nếu không thỏa mãn điều kiện nào
            return false;
        }
    }
}