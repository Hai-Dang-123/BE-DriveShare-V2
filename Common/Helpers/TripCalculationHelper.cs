using Common.DTOs;
using System;

namespace Common.Helpers
{
    public static class TripCalculationHelper
    {
        public static DriverSuggestionDTO CalculateScenarios(
            double distanceKm,
            double rawDrivingHours, // Tổng giờ lái 2 chiều (Khứ hồi)
            double waitTimeHours,
            double bufferHours,
            DateTime pickup,
            DateTime deadline)
        {
            var suggestion = new DriverSuggestionDTO
            {
                DistanceKm = distanceKm,
                EstimatedDurationHours = Math.Round(rawDrivingHours, 1)
            };

            // Deadline window (Thời gian khách cho phép để GIAO HÀNG - 1 chiều)
            double deliveryDeadlineWindow = (deadline - pickup).TotalHours;
            if (deliveryDeadlineWindow <= 0) deliveryDeadlineWindow = 0.1;

            // Tổng thời gian "chết" (Chờ + Buffer)
            double nonDrivingTime = waitTimeHours + bufferHours;

            // Ước tính thời gian cho 1 chiều (để check deadline)
            // Giả sử: Thời gian lái chia đôi, Buffer chia đôi, WaitTime thường nằm ở đầu/cuối nên tạm chia đôi
            double oneWayHoursEst = (rawDrivingHours / 2) + (nonDrivingTime / 2);

            // =====================================================================
            // KỊCH BẢN 1: SOLO (1 TÀI XẾ)
            // =====================================================================
            // 1. Nghỉ ngắn (Break): Cứ 4h lái nghỉ 15p
            double shortBreaks = (rawDrivingHours / 4.0) * 0.25;

            // 2. Nghỉ dài (Sleep): Luật lái max 10h/ngày. 
            double daysNeeded = rawDrivingHours / 10.0;
            int nightsToSleep = (int)Math.Floor(daysNeeded);
            double sleepTime = nightsToSleep * 10.0;

            // Tổng thời gian trôi qua thực tế (Cho cả chuyến đi về)
            double soloElapsedTotal = rawDrivingHours + shortBreaks + sleepTime + nonDrivingTime;

            // Check khả thi về mặt SỨC KHỎE/LUẬT (Không check deadline khách ở đây)
            // Solo: Không được lái quá 10h/ca liên tục (nhưng ở đây đã tính ngủ nghỉ rồi nên ok).
            // Tuy nhiên, nếu chuyến đi kéo dài quá 5-6 ngày liên tục thì Solo là không nên.
            bool isSoloPhysicallyPossible = (rawDrivingHours <= 48); // Chỉ giới hạn nếu tổng giờ lái quá khủng khiếp

            suggestion.SoloScenario = new DriverScenarioDTO
            {
                TotalElapsedHours = Math.Round(soloElapsedTotal, 1),
                DrivingHoursPerDriver = Math.Round(rawDrivingHours, 1),
                IsPossible = isSoloPhysicallyPossible,
                Message = $"1 Tài xế: Tổng {Math.Round(soloElapsedTotal, 1)}h (~{(soloElapsedTotal / 24):N1} ngày)."
            };

            // Warning Logic: Check xem LƯỢT ĐI có kịp deadline không
            // Lượt đi Solo = (Tổng Solo / 2)
            double soloOneWayElapsed = soloElapsedTotal / 2;
            if (soloOneWayElapsed > deliveryDeadlineWindow)
            {
                suggestion.SoloScenario.Note = $"Lưu ý: Có thể trễ giờ giao hàng (Dư kiến đến sau {(soloOneWayElapsed - deliveryDeadlineWindow):N1}h).";
            }

            if (!isSoloPhysicallyPossible) suggestion.SoloScenario.Note = "Quá giới hạn sức khỏe (Cần >48h lái).";


            // =====================================================================
            // KỊCH BẢN 2: TEAM (2 TÀI XẾ)
            // =====================================================================
            // Hiệu suất 95%
            double teamTotalElapsed = (rawDrivingHours / 0.95) + nonDrivingTime;

            suggestion.TeamScenario = new DriverScenarioDTO
            {
                TotalElapsedHours = Math.Round(teamTotalElapsed, 1),
                DrivingHoursPerDriver = Math.Round(rawDrivingHours / 2, 1), // Chia đôi giờ lái
                IsPossible = true, // 2 tài thường luôn chạy được
                Message = $"2 Tài xế: Tổng {Math.Round(teamTotalElapsed, 1)}h."
            };

            // Check Deadline lượt đi
            double teamOneWayElapsed = teamTotalElapsed / 2;
            if (teamOneWayElapsed > deliveryDeadlineWindow)
            {
                suggestion.TeamScenario.Note = "Lưu ý: Có thể giao hàng trễ.";
            }

            // =====================================================================
            // KỊCH BẢN 3: EXPRESS (3 TÀI XẾ)
            // =====================================================================
            // Hiệu suất 98%
            double expressElapsed = (rawDrivingHours / 0.98) + nonDrivingTime;

            suggestion.ExpressScenario = new DriverScenarioDTO
            {
                TotalElapsedHours = Math.Round(expressElapsed, 1),
                DrivingHoursPerDriver = Math.Round(rawDrivingHours / 3, 1),
                IsPossible = true,
                Message = "3 Tài xế (Express)."
            };

            // =====================================================================
            // RECOMMENDATION (ĐỀ XUẤT)
            // =====================================================================
            // Logic: Chọn phương án ít người nhất mà vẫn "IsPossible" (về mặt sức khỏe/luật)
            // Ưu tiên Solo -> Team -> Express

            if (suggestion.SoloScenario.IsPossible)
            {
                suggestion.SystemRecommendation = "SOLO";
                suggestion.RequiredHoursFromQuota = suggestion.SoloScenario.DrivingHoursPerDriver;
            }
            else if (suggestion.TeamScenario.IsPossible)
            {
                suggestion.SystemRecommendation = "TEAM";
                suggestion.RequiredHoursFromQuota = suggestion.TeamScenario.DrivingHoursPerDriver;
            }
            else
            {
                suggestion.SystemRecommendation = "EXPRESS";
                suggestion.RequiredHoursFromQuota = suggestion.ExpressScenario.DrivingHoursPerDriver;
            }

            return suggestion;
        }
    }
}