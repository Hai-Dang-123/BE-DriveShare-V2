using Common.DTOs;
using System;

namespace Common.Helpers
{
    public static class TripCalculationHelper
    {

        // CẬP NHẬT: Thêm tham số waitTimeHours và bufferHours
        public static DriverSuggestionDTO CalculateScenarios(
            double distanceKm,
            double rawDrivingHours,
            double waitTimeHours,   // <--- MỚI
            double bufferHours,     // <--- MỚI
            DateTime pickup,
            DateTime deadline)
        {
            var suggestion = new DriverSuggestionDTO
            {
                DistanceKm = distanceKm,
                EstimatedDurationHours = rawDrivingHours
            };

            // Deadline window (giờ)
            double deadlineWindow = (deadline - pickup).TotalHours;
            if (deadlineWindow <= 0) deadlineWindow = 0.1;

            // Tổng thời gian "chết" (Chờ cấm tải + Buffer)
            double nonDrivingTime = waitTimeHours + bufferHours;

            // =====================================================================
            // KỊCH BẢN 1: SOLO
            // =====================================================================
            double totalBreakTime = (rawDrivingHours / 4.0) * 0.25;
            double daysNeeded = Math.Ceiling(rawDrivingHours / 10.0);
            double totalRestTime = (daysNeeded > 1) ? (daysNeeded - 1) * 10.0 : 0;

            // Tổng thời gian trôi qua
            double soloElapsed = rawDrivingHours + totalBreakTime + totalRestTime + nonDrivingTime;

            suggestion.SoloScenario = new DriverScenarioDTO
            {
                TotalElapsedHours = Math.Round(soloElapsed, 1),
                DrivingHoursPerDriver = Math.Round(rawDrivingHours, 1),
                IsPossible = (soloElapsed <= deadlineWindow) && (rawDrivingHours <= 48),
                Message = $"1 Tài xế: Tổng {Math.Round(soloElapsed, 1)}h (Gồm {Math.Round(nonDrivingTime, 1)}h chờ)."
            };

            if (!suggestion.SoloScenario.IsPossible)
            {
                if (rawDrivingHours > 48) suggestion.SoloScenario.Note = "Quá 48h lái/tuần.";
                else suggestion.SoloScenario.Note = "Trễ deadline.";
            }

            // =====================================================================
            // KỊCH BẢN 2: TEAM
            // =====================================================================
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
            // KỊCH BẢN 3: EXPRESS (3 Tài)
            // =====================================================================
            double expressElapsed = (rawDrivingHours / 0.98) + nonDrivingTime;

            suggestion.ExpressScenario = new DriverScenarioDTO
            {
                TotalElapsedHours = Math.Round(expressElapsed, 1),
                DrivingHoursPerDriver = Math.Round(rawDrivingHours / 3, 1),
                IsPossible = expressElapsed <= deadlineWindow,
                Message = "3 Tài xế (Express)."
            };

            // =====================================================================
            // RECOMMENDATION
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
