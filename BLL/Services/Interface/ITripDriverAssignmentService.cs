using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface ITripDriverAssignmentService
    {
        /// <summary>
        /// (Owner) Tạo một phân công (Assignment) cho Tài xế (nội bộ hoặc thuê ngoài)
        /// và tự động cập nhật TripStatus.
        /// </summary>
        Task<ResponseDTO> CreateAssignmentByOwnerAsync(CreateAssignmentDTO dto);

        /// <summary>
        /// (Driver) Ứng tuyển vào một PostTrip (Bài đăng tìm tài xế của Owner).
        /// </summary>
        Task<ResponseDTO> CreateAssignmentByPostTripAsync(CreateAssignmentByPostTripDTO dto);

        // (Sau này bạn có thể thêm các hàm khác như:
        // Task<ResponseDTO> DriverAcceptAssignmentAsync(Guid assignmentId);
        // Task<ResponseDTO> OwnerCancelAssignmentAsync(Guid assignmentId);
        // )
    }
}
