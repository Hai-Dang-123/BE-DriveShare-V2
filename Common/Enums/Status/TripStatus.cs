using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Enums.Status
{
    public enum TripStatus
    {
        // ───────────────  GIAI ĐOẠN KHỞI TẠO  ───────────────

        CREATED,                     // Chuyến đi vừa được tạo, chưa có tài xế nào
        LOOKING_FOR_DRIVER,          // Chủ xe đang tìm hoặc đăng bài tìm tài xế
        READY_FOR_CONTRACT,          // Đã có đủ tài xế, sẵn sàng tạo hợp đồng
        AWAITING_CONTRACT_SIGNATURE, // Đang chờ chủ xe và tài xế ký hợp đồng

        // ───────────────  GIAI ĐOẠN CHUẨN BỊ / NHẬN XE  ───────────────

        VEHICLE_HANDOVER,            // Tài xế đến nhận xe từ chủ xe để chuẩn bị khởi hành
        LOADING,                     // Đang kiểm hàng / nhận hàng tại điểm xuất phát (Pickup)

        // ───────────────  GIAI ĐOẠN VẬN CHUYỂN  ───────────────

        IN_TRANSIT,                  // Đang trong quá trình vận chuyển hàng hóa
        UNLOADING,                   // Đang dỡ hàng tại điểm giao (End Location)
        DELIVERED,                   // Hàng đã được giao xong, hoàn tất biên bản giao hàng

        // ───────────────  GIAI ĐOẠN KẾT THÚC  ───────────────

        RETURNING_VEHICLE,           // Tài xế đang đưa xe quay về bến hoặc điểm bàn giao
        COMPLETED,                   // Chuyến đi đã hoàn tất toàn bộ (giao hàng + trả xe)

        // ───────────────  HỦY / XÓA  ───────────────

        CANCELLED,                   // Chuyến đi bị hủy trước khi hoàn tất
        DELETED                      // Chuyến đi bị xóa logic khỏi hệ thống
    }

}
