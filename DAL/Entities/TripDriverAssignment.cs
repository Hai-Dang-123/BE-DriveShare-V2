using Common.Enums.Status;
using Common.Enums.Type;
using Common.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class TripDriverAssignment
    {
        public Guid TripDriverAssignmentId { get; set; }

        // --- GỢI Ý BẮT BUỘC (Khóa ngoại - Dựa trên sơ đồ 1-n) ---

        // 1. Phân công này cho Chuyến đi nào?
        public Guid TripId { get; set; } // FK to Trip

        // 2. Phân công này cho Tài xế nào?
        public Guid DriverId { get; set; } // FK to Driver

        // --- Chi tiết phân công ---
        public DriverType Type { get; set; } // Chính hay Phụ
        public DateTime CreateAt { get; set; } = DateTime.Now;
        public DateTime UpdateAt { get; set; } = DateTime.Now;

        // --- Chi phí trả cho tài xế ---
        public decimal BaseAmount { get; set; }   // Số tiền cơ bản
        public decimal? BonusAmount { get; set; }  // Phụ phí

        // GỢI Ý: Cấu hình EF Core để bỏ qua trường này (NotMapped)
        public decimal TotalAmount => BaseAmount + (BonusAmount ?? 0);

        // --- Lộ trình của tài xế (Có thể khác lộ trình chính của Trip) ---
        public Location StartLocation { get; set; }
        public Location EndLocation { get; set; }

        

        // [NEW] Số tiền cọc yêu cầu
        public decimal DepositAmount { get; set; } = 0;

        // [NEW] Trạng thái cọc
        public DepositStatus DepositStatus { get; set; } // Pending, Deposited, Refunded, Seized (Bị tịch thu)

        // [NEW] Thời điểm nộp cọc
        public DateTime? DepositAt { get; set; }


        // =========================================================
        // 1. CHECK-IN (LÊN XE / BẮT ĐẦU)
        // =========================================================
        public bool IsOnBoard { get; set; } = false; // Đã lên xe chưa?
        public DateTime? OnBoardTime { get; set; }     // Thời gian bấm
        public string? OnBoardLocation { get; set; }   // Tọa độ GPS & Địa chỉ
        public string? OnBoardImage { get; set; }      // Ảnh selfie/taplo xe
        // [NEW] Lưu Note cảnh báo (Ví dụ: "Lệch 5km")
        public string? CheckInNote { get; set; }

        // =========================================================
        // 2. CHECK-OUT (XUỐNG XE / KẾT THÚC)
        // =========================================================
        public bool IsFinished { get; set; } = false;  // Đã xong việc chưa?
        public DateTime? OffBoardTime { get; set; }    // Thời gian bấm
        public string? OffBoardLocation { get; set; }  // Tọa độ GPS & Địa chỉ
        public string? OffBoardImage { get; set; }     // Ảnh xác nhận
        // [NEW] Lưu Note cảnh báo
        public string? CheckOutNote { get; set; }

        // --- Trạng thái ---
        public AssignmentStatus AssignmentStatus { get; set; } // Offered, Accepted, Rejected, Completed...
        public DriverPaymentStatus PaymentStatus { get; set; } // Unpaid, Paid

        // --- GỢI Ý BẮT BUỘC (Thuộc tính điều hướng) ---
        public virtual Trip Trip { get; set; } = null!;
        public virtual Driver Driver { get; set; } = null!;
    }
}