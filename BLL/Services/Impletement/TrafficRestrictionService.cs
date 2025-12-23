using BLL.Services.Interface;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;
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
                if (string.IsNullOrWhiteSpace(address)) return (false, 0, null);

                // 1. Chuẩn hóa thời gian & địa chỉ
                DateTime entryTimeVN = estimatedEntryTime;
                var entryTimeOfDay = entryTimeVN.TimeOfDay;
                string normalizedAddress = address.ToLower().Trim();

                // 2. Load Data (Nên cache layer này)
                var restrictions = await _unitOfWork.TruckRestrictionRepo.GetAll().AsNoTracking().ToListAsync();

                // 3. Match Logic (Đã Fix lỗi qua đêm)
                var matchedRestriction = restrictions.FirstOrDefault(r =>
                {
                    // A. Check Giờ
                    bool isTimeRestricted = false;
                    if (r.BanStartTime < r.BanEndTime)
                    {
                        // Cấm trong ngày (7h -> 9h)
                        isTimeRestricted = (entryTimeOfDay >= r.BanStartTime && entryTimeOfDay < r.BanEndTime);
                    }
                    else
                    {
                        // Cấm qua đêm (22h -> 6h sáng hôm sau)
                        isTimeRestricted = (entryTimeOfDay >= r.BanStartTime || entryTimeOfDay < r.BanEndTime);
                    }

                    if (!isTimeRestricted) return false;

                    // B. Check Keyword Address
                    if (!string.IsNullOrEmpty(r.MatchKeywords))
                    {
                        var keywords = r.MatchKeywords.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        return keywords.Any(k => normalizedAddress.Contains(k.Trim().ToLower()));
                    }

                    // C. Fallback ZoneName
                    return !string.IsNullOrEmpty(r.ZoneName) && normalizedAddress.Contains(r.ZoneName.ToLower().Trim());
                });

                // 4. Trả kết quả
                if (matchedRestriction != null)
                {
                    DateTime allowedTimeVN;

                    // Tính thời gian được phép đi
                    if (matchedRestriction.BanStartTime < matchedRestriction.BanEndTime)
                    {
                        allowedTimeVN = entryTimeVN.Date.Add(matchedRestriction.BanEndTime);
                    }
                    else
                    {
                        // Xử lý cấm qua đêm
                        if (entryTimeOfDay >= matchedRestriction.BanStartTime)
                            allowedTimeVN = entryTimeVN.Date.AddDays(1).Add(matchedRestriction.BanEndTime);
                        else
                            allowedTimeVN = entryTimeVN.Date.Add(matchedRestriction.BanEndTime);
                    }

                    double waitHours = (allowedTimeVN - entryTimeVN).TotalHours;
                    if (waitHours < 0) waitHours = 0;

                    return (true, waitHours, matchedRestriction.Description);
                }

                return (false, 0, null);
            }
            catch (Exception ex)
            {
                return (false, 0, null);
            }
        }
    }
}