using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Helpers
{
    public static class TripCalculationHelper
    {
        public static DriverSuggestionDTO CalculateScenarios(double distance, double driveHours, DateTime pickup, DateTime deadline)
        {
            var suggestion = new DriverSuggestionDTO
            {
                DistanceKm = distance,
                EstimatedDurationHours = driveHours
            };

            // Tính tổng thời gian cho phép (Deadline)
            double deadlineHours = (deadline - pickup).TotalHours;

            // =========================================================
            // KỊCH BẢN 1: SOLO (1 TÀI XẾ)
            // =========================================================
            // Luật giả định: Lái tối đa 10h/ngày. Phải nghỉ ngơi ~14h (ăn, ngủ, vệ sinh) cho mỗi chu kỳ 24h.
            double soloDays = Math.Ceiling(driveHours / 10.0);

            // Công thức: Giờ lái + (Số ngày - 1) * 14h nghỉ.
            // (Trừ ngày cuối cùng vì đến nơi là xong, không cần cộng giờ ngủ đêm cuối)
            double soloTotalTime = driveHours + ((soloDays - 1) * 14.0);

            suggestion.SoloScenario = new DriverScenarioDTO
            {
                TotalHoursNeeded = Math.Round(soloTotalTime, 1),

                // Với 1 tài xế, họ chịu trách nhiệm toàn bộ thời gian chuyến đi
                WorkHoursPerDriver = Math.Round(soloTotalTime, 1),

                IsPossible = soloTotalTime <= deadlineHours,
                Message = $"1 Tài xế: Chạy ngày nghỉ đêm. Mất khoảng {soloDays} ngày."
            };

            if (!suggestion.SoloScenario.IsPossible)
            {
                double delay = soloTotalTime - deadlineHours;
                suggestion.SoloScenario.Note = $"Dự kiến trễ khoảng {delay:F1} giờ so với thời gian yêu cầu.";
            }
            else
            {
                suggestion.SoloScenario.Note = "Kịp tiến độ. Tiết kiệm chi phí nhân sự nhất.";
            }

            // =========================================================
            // KỊCH BẢN 2: TEAM (2 TÀI XẾ)
            // =========================================================
            // Xe chạy liên tục, chỉ dừng ăn uống/đổ dầu nhanh. Hiệu suất ~95% so với lái thuần.
            double teamTotalTime = driveHours / 0.95;

            suggestion.TeamScenario = new DriverScenarioDTO
            {
                TotalHoursNeeded = Math.Round(teamTotalTime, 1),

                // Chia đôi thời gian công tác (mỗi người chịu 50% hành trình)
                WorkHoursPerDriver = Math.Round(teamTotalTime / 2, 1),

                IsPossible = teamTotalTime <= deadlineHours,
                Message = "2 Tài xế: Thay phiên lái liên tục. Xe không nghỉ đêm."
            };

            if (!suggestion.TeamScenario.IsPossible)
            {
                suggestion.TeamScenario.Note = "Vẫn trễ giờ dù chạy 2 tài (Deadline quá gấp).";
            }
            else
            {
                suggestion.TeamScenario.Note = "Kịp tiến độ. Phương án cân bằng tốt nhất.";
            }

            // =========================================================
            // KỊCH BẢN 3: EXPRESS (3 TÀI XẾ)
            // =========================================================
            // Chạy "đua", giảm thiểu tối đa thời gian dừng. Hiệu suất ~98%.
            double expressTotalTime = driveHours / 0.98;

            suggestion.ExpressScenario = new DriverScenarioDTO
            {
                TotalHoursNeeded = Math.Round(expressTotalTime, 1),

                // Chia 3 thời gian công tác
                WorkHoursPerDriver = Math.Round(expressTotalTime / 3, 1),

                IsPossible = expressTotalTime <= deadlineHours,
                Message = "3 Tài xế: Chạy siêu tốc (Express). Dành cho hàng gấp/lạnh."
            };

            if (suggestion.ExpressScenario.IsPossible)
            {
                suggestion.ExpressScenario.Note = "Giao hàng nhanh nhất có thể.";
            }

            // =========================================================
            // HỆ THỐNG ĐƯA RA LỜI KHUYÊN (RECOMMENDATION)
            // =========================================================
            if (suggestion.SoloScenario.IsPossible)
            {
                suggestion.SystemRecommendation = "Kịch bản 1 TÀI XẾ là đủ để kịp giờ và tiết kiệm chi phí nhất.";
            }
            else if (suggestion.TeamScenario.IsPossible)
            {
                suggestion.SystemRecommendation = "Khuyên dùng 2 TÀI XẾ (Team) để đảm bảo tiến độ và an toàn sức khỏe tài xế.";
            }
            else if (suggestion.ExpressScenario.IsPossible)
            {
                suggestion.SystemRecommendation = "Hàng quá gấp! Bắt buộc phải dùng 3 TÀI XẾ (Express) mới kịp.";
            }
            else
            {
                suggestion.SystemRecommendation = "CẢNH BÁO: Deadline quá gấp, không thể chạy kịp kể cả với 3 tài xế!";
            }

            return suggestion;
        }
    }
}
