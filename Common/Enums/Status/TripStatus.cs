using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Enums.Status
{
    /// <summary>
    /// Định nghĩa các trạng thái của một Chuyến đi (Trip),
    /// bao gồm cả luồng Hợp đồng Provider-Owner và Owner-Driver.
    /// </summary>
    public enum TripStatus
    {
        // ─────────────── GIAI ĐOẠN 1: KHỞI TẠO & HỢP ĐỒNG (PROVIDER <-> OWNER) ───────────────

        /// <summary>
        /// (Khởi tạo) - Chuyến đi vừa được tạo, thường là từ một PostTrip của Provider.
        /// </summary>
        CREATED,

        /// <summary>
        /// (Đợi HĐ Provider) - Đang chờ Owner và Provider ký hợp đồng (TripProviderContract).
        /// </summary>
        AWAITING_PROVIDER_CONTRACT,

        /// <summary>
        /// (Đợi Provider thanh toán) - HĐ Provider đã ký, đang đợi Provider thanh toán (cọc/toàn bộ)
        /// cho Owner hoặc Hệ thống.
        /// </summary>
        AWAITING_PROVIDER_PAYMENT,

        // ─────────────── GIAI ĐOẠN 2: TÌM & GÁN TÀI XẾ (OWNER <-> DRIVER) ───────────────

        AWAITING_OWNER_CONTRACT,

        /// <summary>
        /// (Chờ gán tài xế) - Owner đã sẵn sàng. Trạng thái này bao gồm cả việc
        /// Owner "Đăng bài tìm tài xế" (Posting for driver) hoặc "Gán tài xế" (Assigning internal driver).
        /// </summary>
        PENDING_DRIVER_ASSIGNMENT,


        /// <summary>
        /// (Đợi HĐ Tài xế) - Áp dụng khi thuê tài xế ngoài. Đã tìm thấy tài xế,
        /// đang đợi Owner và Driver ký hợp đồng (DriverContract).
        /// </summary>
        AWAITING_DRIVER_CONTRACT,

        /// <summary>
        /// ( Đợi Owner thanh toán tài xế ) - HĐ Tài xế đã ký, đang đợi Owner
        /// Sau khi Owner và Driver ký hợp đồng, Owner cần thanh toán cho Driver (cọc/toàn bộ)
        /// </summary>
        AWAITING_OWNER_PAYMENT,

        // ─────────────── GIAI ĐOẠN 3: CHUẨN BỊ VẬN HÀNH ───────────────

        /// <summary>
        /// (Sẵn sàng lấy xe) - Đã có tài xế (đã ký HĐ hoặc đã gán),
        /// sẵn sàng cho bước tiếp theo là tài xế đi lấy xe.
        /// </summary>
        READY_FOR_VEHICLE_HANDOVER,

        /// <summary>
        /// (Đang bàn giao xe) - Tài xế đang trong quá trình nhận xe từ Chủ xe (Owner).
        /// </summary>
        VEHICLE_HANDOVER,

        /// <summary>
        /// (Đang ĐI lấy hàng) - Tài xế đang trên đường đi lấy hàng
        /// </summary>
        MOVING_TO_PICKUP,

        /// <summary>
        /// (Đang lấy hàng) - Tài xế đã lấy xe, đang trên đường đến điểm lấy hàng (của Provider).
        /// </summary>
        LOADING,

        // ─────────────── GIAI ĐOẠN 4: VẬN HÀNH ───────────────

        /// <summary>
        /// (Đang vận chuyển) - Tài xế đã lấy hàng, đang trên đường giao hàng.
        /// </summary>
        MOVING_TO_DROPOFF,

        /// <summary>
        /// (Đang giao hàng) - Đã đến điểm dỡ hàng, đang trong quá trình giao hàng
        /// và làm biên bản giao nhận.
        /// </summary>
        UNLOADING,

        /// <summary>
        /// (Đã giao hàng) - Giao hàng thành công, đã có biên bản ký nhận.
        /// Sẵn sàng cho bước trả xe.
        /// </summary>
        DELIVERED,

        // ─────────────── GIAI ĐOẠN 5: KẾT THÚC & TRẢ XE ───────────────

        /// <summary>
        /// (Đang trả xe) - Tài xế đang trên đường mang xe về trả cho Chủ xe (Owner).
        /// </summary>
        RETURNING_VEHICLE,

        /// <summary>
        /// (Đã trả xe) - Tài xế đã hoàn tất trả xe cho Owner,
        /// chuyến đi về mặt vận hành đã kết thúc.
        /// </summary>
        VEHICLE_RETURNED,

        // ─────────────── GIAI ĐOẠN 6: QUYẾT TOÁN & THANH TOÁN ───────────────

        /// <summary>
        /// (Đợi Provider quyết toán) - Chờ thanh toán cuối cùng từ Provider
        /// (tiền trong hệ thống) chuyển về cho Owner.
        /// </summary>
        AWAITING_FINAL_PROVIDER_PAYOUT,

        /// <summary>
        /// (Đợi thanh toán tài xế) - Owner đã nhận tiền, đang đợi Owner
        /// (hoặc Hệ thống) thanh toán cho Tài xế.
        /// </summary>
        AWAITING_FINAL_DRIVER_PAYOUT,

        /// <summary>
        /// (Hoàn tất) - Tất cả các bên đã được thanh toán. Chuyến đi chính thức kết thúc.
        /// </summary>
        COMPLETED,

        // ─────────────── TRẠNG THÁI NGOẠI LỆ ───────────────

        /// <summary>
        /// (Đã hủy) - Chuyến đi bị hủy ở một trong các giai đoạn trên.
        /// </summary>
        CANCELLED,

        /// <summary>
        /// ( soft delete ) - Chuyến đi bị xóa mềm khỏi hệ thống.
        /// </summary>
        /// 
        DELETED
    }
}