using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IDriverWorkSessionService
    {
        Task<ResponseDTO> StartSessionAsync(StartSessionDTO dto);
        Task<ResponseDTO> EndSessionAsync(EndSessionDTO dto);

        // Không cần truyền ID, tự lấy từ token
        Task<ResponseDTO> CheckDriverEligibilityAsync();
        Task<ResponseDTO> GetDriverHistoryAsync(DriverHistoryFilterDTO filter);
        Task<ResponseDTO> GetCurrentSessionInTripAsync(Guid tripId);
        Task<ResponseDTO> ImportDriverHistoryAsync(ImportDriverHistoryDTO dto);
        Task<DriverAvailabilityDTO> CheckDriverAvailabilityAsync(Guid driverId); // <--- Thêm hàm này

        Task<ResponseDTO> GetDriverCurrentAvailabilityAsync();
        Task<ResponseDTO> ValidateDriverForTripAsync(Guid tripId);
    }
}
