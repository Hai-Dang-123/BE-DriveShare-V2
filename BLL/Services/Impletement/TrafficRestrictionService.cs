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
                if (string.IsNullOrWhiteSpace(address))
                    return (false, 0, null);

                // 1. CHUYỂN ĐỔI MÚI GIỜ (CHUẨN HÓA)
                // Quy ước: Nếu nhận UTC thì +7. Nếu nhận Unspecified thì coi như là giờ VN luôn (Local).
                DateTime entryTimeVN = estimatedEntryTime;

                //if (estimatedEntryTime.Kind == DateTimeKind.Utc)
                //{
                //    entryTimeVN = estimatedEntryTime.AddHours(7);
                //}

                var entryTimeOfDay = entryTimeVN.TimeOfDay;

                // Chuẩn hóa địa chỉ đầu vào: chữ thường, trim
                string normalizedAddress = address.ToLower().Trim();

                // 2. LẤY DATA (Có thể Cache layer này nếu muốn nhanh hơn nữa)
                // Lấy tất cả luật ra để lọc trong memory (Vì bảng này thường ít dòng, < 100 dòng thì load hết OK)
                var restrictions = await _unitOfWork.TruckRestrictionRepo.GetAll().AsNoTracking().ToListAsync();

                // 3. TÌM RULE PHÙ HỢP (LOGIC MATCHING KEYWORD)
                var matchedRestriction = restrictions.FirstOrDefault(r =>
                {
                    // A. Check Giờ trước (Nhanh nhất)
                    if (entryTimeOfDay < r.BanStartTime || entryTimeOfDay >= r.BanEndTime)
                        return false;

                    // B. Check Địa điểm dựa trên Keyword (Dynamic)
                    if (!string.IsNullOrEmpty(r.MatchKeywords))
                    {
                        // Tách chuỗi keyword từ DB: "hoàn kiếm, đống đa" -> ["hoàn kiếm", "đống đa"]
                        var keywords = r.MatchKeywords.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                        // Nếu địa chỉ chứa BẤT KỲ từ khóa nào -> Dính
                        foreach (var key in keywords)
                        {
                            if (normalizedAddress.Contains(key.Trim().ToLower()))
                            {
                                return true; // Match!
                            }
                        }
                    }
                    else
                    {
                        // Fallback: Nếu không có keyword, so sánh ZoneName như cũ
                        if (!string.IsNullOrEmpty(r.ZoneName) && normalizedAddress.Contains(r.ZoneName.ToLower().Trim()))
                        {
                            return true;
                        }
                    }

                    return false;
                });

                // 4. TRẢ KẾT QUẢ
                if (matchedRestriction != null)
                {
                    // Tính thời gian chờ
                    // Lấy ngày hiện tại + Giờ kết thúc cấm
                    var allowedTimeVN = entryTimeVN.Date.Add(matchedRestriction.BanEndTime);

                    // Nếu giờ kết thúc nhỏ hơn giờ hiện tại (trường hợp qua đêm), cộng thêm 1 ngày (nhưng ở đây logic TimeOfDay đã chặn rồi, safety check thôi)
                    if (allowedTimeVN < entryTimeVN) allowedTimeVN = allowedTimeVN.AddDays(1);

                    var waitTimeSpan = allowedTimeVN - entryTimeVN;
                    var waitHours = waitTimeSpan.TotalHours;

                    if (waitHours < 0) waitHours = 0;

                    return (true, waitHours, matchedRestriction.Description);
                }

                return (false, 0, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error CheckRestriction: {ex.Message}");
                return (false, 0, null);
            }
        }
    }
}