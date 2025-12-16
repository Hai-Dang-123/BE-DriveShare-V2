using BLL.Services.Interface;
using DAL.UnitOfWork;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class TrafficRestrictionService : ITrafficRestrictionService
    {
        private readonly IUnitOfWork _unitOfWork;

        public TrafficRestrictionService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<(bool IsRestricted, double WaitTime, string Reason)> CheckRestrictionAsync(string address, DateTime estimatedEntryTime)
        {
            try
            {
                // 1. CHUYỂN ĐỔI MÚI GIỜ (QUAN TRỌNG)
                // Lưu ý: DB lưu giờ cấm theo giờ Việt Nam (ví dụ 06:00, 16:00)
                // Nếu input là UTC, phải cộng 7 tiếng để ra giờ địa phương
                DateTime entryTimeVN = estimatedEntryTime;

                // Kiểm tra nếu là UTC hoặc Unspecified (thường BE nhận từ FE là UTC)
                if (estimatedEntryTime.Kind == DateTimeKind.Utc)
                {
                    //entryTimeVN = estimatedEntryTime.AddHours(7);
                    entryTimeVN = estimatedEntryTime;

                }

                var entryTimeOfDay = entryTimeVN.TimeOfDay;

                // 2. Lấy danh sách Restrictions (Nên dùng caching nếu có thể)
                var restrictions = await _unitOfWork.TruckRestrictionRepo.ToListAsync();

                // 3. Tìm Rule phù hợp
                var matchedRestriction = restrictions.FirstOrDefault(r =>
                    !string.IsNullOrEmpty(address) &&
                    !string.IsNullOrEmpty(r.ZoneName) &&
                    // Chuẩn hóa chuỗi: Lowercase và Trim để so sánh chính xác hơn
                    address.ToLower().Contains(r.ZoneName.ToLower().Trim()) &&
                    entryTimeOfDay >= r.BanStartTime &&
                    entryTimeOfDay < r.BanEndTime
                );

                if (matchedRestriction != null)
                {
                    // 4. Tính thời gian chờ
                    // Lấy ngày (VN) + Giờ kết thúc cấm
                    var allowedTimeVN = entryTimeVN.Date.Add(matchedRestriction.BanEndTime);

                    // Tính độ lệch thời gian
                    var waitTimeSpan = allowedTimeVN - entryTimeVN;
                    var waitHours = waitTimeSpan.TotalHours;

                    // Đảm bảo không âm (safety check)
                    if (waitHours < 0) waitHours = 0;

                    return (true, waitHours, matchedRestriction.Description);
                }

                return (false, 0, null);
            }
            catch (Exception ex)
            {
                // Log lỗi (Console hoặc Logger) nhưng không làm crash luồng chính
                Console.WriteLine($"Error CheckRestriction: {ex.Message}");
                return (false, 0, null);
            }
        }
    }
}