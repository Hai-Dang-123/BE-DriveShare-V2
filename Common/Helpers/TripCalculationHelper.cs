using Common.DTOs;
using System;

namespace Common.Helpers
{
    public static class TripCalculationHelper
    {
        public static DriverSuggestionDTO CalculateScenarios(double distanceKm, double rawDrivingHours, DateTime pickup, DateTime deadline)
        {
            var suggestion = new DriverSuggestionDTO
            {
                DistanceKm = distanceKm,
                EstimatedDurationHours = rawDrivingHours
            };

            // Deadline cho phép (tính bằng giờ)
            double deadlineWindow = (deadline - pickup).TotalHours;
            if (deadlineWindow <= 0) deadlineWindow = 0.1; // Tránh chia cho 0

            // =====================================================================
            // KỊCH BẢN 1: SOLO (1 TÀI XẾ)
            // =====================================================================
            // Luật giả định: Lái 10h/ngày. Nghỉ 15p mỗi 4h.
            double totalBreakTime = (rawDrivingHours / 4.0) * 0.25;
            double daysNeeded = Math.Ceiling(rawDrivingHours / 10.0);
            double totalRestTime = (daysNeeded > 1) ? (daysNeeded - 1) * 10.0 : 0;

            double soloElapsed = rawDrivingHours + totalBreakTime + totalRestTime;

            suggestion.SoloScenario = new DriverScenarioDTO
            {
                TotalElapsedHours = Math.Round(soloElapsed, 1),
                DrivingHoursPerDriver = Math.Round(rawDrivingHours, 1), // Gánh 100%

                // Check: Kịp deadline VÀ Không quá 48h lái (Luật tuần)
                IsPossible = (soloElapsed <= deadlineWindow) && (rawDrivingHours <= 48),
                Message = $"1 Tài xế: Cần {daysNeeded} ngày (bao gồm nghỉ ngơi)."
            };

            if (!suggestion.SoloScenario.IsPossible)
            {
                if (rawDrivingHours > 48) suggestion.SoloScenario.Note = "Vượt quá giới hạn 48h lái/tuần.";
                else suggestion.SoloScenario.Note = "Không kịp thời gian giao hàng.";
            }

            // =====================================================================
            // KỊCH BẢN 2: TEAM (2 TÀI XẾ)
            // =====================================================================
            // Xe chạy liên tục, chỉ dừng đổi tài/đổ xăng (Hiệu suất 95%)
            double teamElapsed = rawDrivingHours / 0.95;

            suggestion.TeamScenario = new DriverScenarioDTO
            {
                TotalElapsedHours = Math.Round(teamElapsed, 1),
                DrivingHoursPerDriver = Math.Round(rawDrivingHours / 2, 1), // Chia đôi gánh nặng

                // Check: Kịp deadline
                IsPossible = teamElapsed <= deadlineWindow,
                Message = "2 Tài xế (Team): Chạy luân phiên liên tục."
            };

            if (!suggestion.TeamScenario.IsPossible)
                suggestion.TeamScenario.Note = "Vẫn trễ deadline (Cần xem xét Express).";

            // =====================================================================
            // [BỔ SUNG] KỊCH BẢN 3: EXPRESS (3 TÀI XẾ)
            // =====================================================================
            // Dành cho hàng siêu gấp hoặc đường rất dài. Hiệu suất tối đa 98%.
            double expressElapsed = rawDrivingHours / 0.98;

            suggestion.ExpressScenario = new DriverScenarioDTO
            {
                TotalElapsedHours = Math.Round(expressElapsed, 1),
                DrivingHoursPerDriver = Math.Round(rawDrivingHours / 3, 1), // Chia 3 gánh nặng

                IsPossible = expressElapsed <= deadlineWindow,
                Message = "3 Tài xế (Express): Chạy tốc độ tối đa, không dừng nghỉ."
            };

            if (!suggestion.ExpressScenario.IsPossible)
                suggestion.ExpressScenario.Note = "Deadline bất khả thi (Cần dời lịch giao).";

            // =====================================================================
            // KẾT LUẬN (RECOMMENDATION)
            // Mặc định là giờ gốc (nếu không cách nào chạy được)
            suggestion.RequiredHoursFromQuota = rawDrivingHours;

            if (suggestion.SoloScenario.IsPossible)
            {
                suggestion.SystemRecommendation = "SOLO";
                // Solo thì 1 người gánh hết 100%
                suggestion.RequiredHoursFromQuota = suggestion.SoloScenario.DrivingHoursPerDriver;
            }
            else if (suggestion.TeamScenario.IsPossible)
            {
                suggestion.SystemRecommendation = "TEAM";
                // Team thì chia đôi
                suggestion.RequiredHoursFromQuota = suggestion.TeamScenario.DrivingHoursPerDriver;
            }
            else if (suggestion.ExpressScenario.IsPossible)
            {
                suggestion.SystemRecommendation = "EXPRESS";
                // Express chia ba
                suggestion.RequiredHoursFromQuota = suggestion.ExpressScenario.DrivingHoursPerDriver;
            }
            else
            {
                suggestion.SystemRecommendation = "IMPOSSIBLE";
                // Nếu không chạy được, gán giá trị nhỏ nhất có thể (Team) để hiển thị tham khảo
                suggestion.RequiredHoursFromQuota = suggestion.TeamScenario.DrivingHoursPerDriver;
            }

            return suggestion;
        }
    }
}