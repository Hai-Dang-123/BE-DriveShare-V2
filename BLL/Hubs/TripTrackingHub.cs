using BLL.Services.Impletement;
using DAL.UnitOfWork;
using DAL.Entities;
using Common.Enums.Status;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using BLL.Utilities; // ✅ Using Utility

namespace BLL.Hubs
{
    [Authorize]
    public class TripTrackingHub : Hub
    {
        private readonly LocationCacheService _cacheService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility; // ✅ Inject Utility

        public TripTrackingHub(
            LocationCacheService cacheService,
            IUnitOfWork unitOfWork,
            UserUtility userUtility) // ✅ Inject vào Constructor
        {
            _cacheService = cacheService;
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
        }

        // =========================================================================
        // 1. JOIN GROUP
        // =========================================================================
        public async Task JoinTripGroup(Guid tripId)
        {
            // ✅ Dùng UserUtility để lấy ID từ Context.User của SignalR
            var currentUserId = _userUtility.GetUserId(Context.User);

            if (currentUserId == Guid.Empty)
            {
                throw new HubException("Unauthorized: Invalid Token or User ID not found");
            }

            // Check quyền
            bool isAllowed = await CheckPermissionAsync(currentUserId, tripId);
            if (!isAllowed)
            {
                throw new HubException("Forbidden: Bạn không có quyền theo dõi chuyến đi này.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"TRIP_{tripId}");

            // Trả cache
            var lastLoc = _cacheService.GetLocation(tripId);
            if (lastLoc != null)
            {
                await Clients.Caller.SendAsync("ReceiveLocation", lastLoc);
            }
        }

        // =========================================================================
        // 2. SEND LOCATION
        // =========================================================================
        public async Task SendLocationUpdate(Guid tripId, double lat, double lng, double bearing, double speed)
        {
            // ✅ Dùng UserUtility để lấy tên (nếu bạn đã thêm hàm GetUserName như ở trên)
            // Hoặc vẫn dùng _userUtility.GetUserId(Context.User) để query tên thật từ DB nếu cần
            var driverName = _userUtility.GetUserName(Context.User, "Tài xế");

            var data = new
            {
                Lat = lat,
                Lng = lng,
                Bearing = bearing,
                Speed = speed,
                DriverName = driverName,
                UpdatedAt = DateTime.UtcNow
            };

            _cacheService.UpdateLocation(tripId, data);

            await Clients.GroupExcept($"TRIP_{tripId}", Context.ConnectionId)
                         .SendAsync("ReceiveLocation", data);
        }

        // =========================================================================
        // 3. LEAVE GROUP
        // =========================================================================
        public async Task LeaveTripGroup(Guid tripId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"TRIP_{tripId}");
        }

        // =========================================================================
        // CHECK PERMISSION
        // =========================================================================
        private async Task<bool> CheckPermissionAsync(Guid userId, Guid tripId)
        {
            // ✅ Dùng UserUtility để lấy Role
            var role = _userUtility.GetUserRole(Context.User);

            if (!string.IsNullOrEmpty(role) && (role.ToUpper() == "ADMIN" || role.ToUpper() == "STAFF")) return true;

            var trip = await _unitOfWork.TripRepo.GetAll()
                .AsNoTracking()
                .Include(t => t.TripProviderContract)
                .Include(t => t.DriverAssignments)
                .FirstOrDefaultAsync(t => t.TripId == tripId);

            if (trip == null) return false;

            if (trip.OwnerId == userId) return true;

            if (trip.TripProviderContract != null && trip.TripProviderContract.CounterpartyId == userId) return true;

            var isAssignedDriver = trip.DriverAssignments
                .Any(a => a.DriverId == userId && a.AssignmentStatus == AssignmentStatus.ACCEPTED);

            if (isAssignedDriver) return true;

            return false;
        }
    }
}