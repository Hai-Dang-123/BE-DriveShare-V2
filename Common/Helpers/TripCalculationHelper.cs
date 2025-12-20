using Common.DTOs;
using System;

namespace Common.Helpers
{
    public static class TripCalculationHelper
    {
        public static DriverSuggestionDTO CalculateScenarios(
            double distanceKm,
            double rawDrivingHours,
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

            // Deadline window (giờ)
            double deadlineWindow = (deadline - pickup).TotalHours;
            if (deadlineWindow <= 0) deadlineWindow = 0.1;

            // Tổng thời gian "chết" (Chờ kho + Buffer rủi ro)
            double nonDrivingTime = waitTimeHours + bufferHours;

            // =====================================================================
            // KỊCH BẢN 1: SOLO (1 TÀI XẾ)
            // =====================================================================
            // 1. Nghỉ ngắn (Break): Cứ 4h lái nghỉ 15p
            double shortBreaks = (rawDrivingHours / 4.0) * 0.25;

            // 2. Nghỉ dài (Sleep): Luật lái max 10h/ngày. 
            // Ví dụ: 34h lái -> 3.4 ngày -> Cần ngủ 3 đêm (mỗi đêm 10 tiếng gồm ăn tối/ngủ/ăn sáng)
            double daysNeeded = rawDrivingHours / 10.0;
            int nightsToSleep = (int)Math.Floor(daysNeeded);
            // Nếu phần dư > 0 (ví dụ lái 10.5h), thực tế tài xế sẽ cố hoặc ngủ thêm, 
            // nhưng công thức này lấy sàn để tính tối thiểu số đêm phải ngủ lại dọc đường.

            double sleepTime = nightsToSleep * 10.0;

            // Tổng thời gian trôi qua thực tế
            double soloElapsed = rawDrivingHours + shortBreaks + sleepTime + nonDrivingTime;

            suggestion.SoloScenario = new DriverScenarioDTO
            {
                TotalElapsedHours = Math.Round(soloElapsed, 1),
                DrivingHoursPerDriver = Math.Round(rawDrivingHours, 1),
                IsPossible = (soloElapsed <= deadlineWindow) && (rawDrivingHours <= 48),
                Message = $"1 Tài xế: Tổng {Math.Round(soloElapsed, 1)}h (~{(soloElapsed / 24):N1} ngày)."
            };

            if (!suggestion.SoloScenario.IsPossible)
            {
                if (rawDrivingHours > 48) suggestion.SoloScenario.Note = "Quá 48h lái/tuần.";
                else suggestion.SoloScenario.Note = "Trễ deadline (Cần thêm tài).";
            }

            // =====================================================================
            // KỊCH BẢN 2: TEAM (2 TÀI XẾ CHẠY SUỐT)
            // =====================================================================
            // Hiệu suất 95% (đổi tài, vệ sinh)
            double teamTotalElapsed = (rawDrivingHours / 0.95) + nonDrivingTime;

            suggestion.TeamScenario = new DriverScenarioDTO
            {
                TotalElapsedHours = Math.Round(teamTotalElapsed, 1),
                DrivingHoursPerDriver = Math.Round(rawDrivingHours / 2, 1),
                IsPossible = teamTotalElapsed <= deadlineWindow,
                Message = $"2 Tài xế: Tổng {Math.Round(teamTotalElapsed, 1)}h."
            };

            if (!suggestion.TeamScenario.IsPossible)
                suggestion.TeamScenario.Note = "Vẫn trễ deadline.";

            // =====================================================================
            // KỊCH BẢN 3: EXPRESS (3 TÀI XẾ)
            // =====================================================================
            // Hiệu suất 98%
            double expressElapsed = (rawDrivingHours / 0.98) + nonDrivingTime;

            suggestion.ExpressScenario = new DriverScenarioDTO
            {
                TotalElapsedHours = Math.Round(expressElapsed, 1),
                DrivingHoursPerDriver = Math.Round(rawDrivingHours / 3, 1),
                IsPossible = expressElapsed <= deadlineWindow,
                Message = "3 Tài xế (Express)."
            };

            // =====================================================================
            // RECOMMENDATION (ĐỀ XUẤT)
            // =====================================================================
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
            else if (suggestion.ExpressScenario.IsPossible)
            {
                suggestion.SystemRecommendation = "EXPRESS";
                suggestion.RequiredHoursFromQuota = suggestion.ExpressScenario.DrivingHoursPerDriver;
            }
            else
            {
                suggestion.SystemRecommendation = "IMPOSSIBLE";
                suggestion.RequiredHoursFromQuota = suggestion.TeamScenario.DrivingHoursPerDriver;
            }

            return suggestion;
        }
    }
}