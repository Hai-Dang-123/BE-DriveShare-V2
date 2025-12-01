using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Enums.Status
{
    public enum IssueStatus
    {
        [Description("Mới báo cáo")]
        REPORTED = 1,   // Mới tạo, chưa xác nhận

        [Description("Đã xác nhận")]
        CONFIRMED = 2,  // Hai bên đã đồng ý lỗi này

        [Description("Đang tranh chấp")]
        DISPUTED = 3,   // Một bên không đồng ý (vd: cãi là lỗi cũ)

        [Description("Đã xử lý/Đền bù")]
        RESOLVED = 4,   // Đã sửa xong hoặc đã đền tiền

        [Description("Đã hủy")]
        CANCELLED = 5   // Báo nhầm hoặc bỏ qua
    }
}
