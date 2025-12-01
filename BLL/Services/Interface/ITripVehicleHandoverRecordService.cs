using Common.DTOs;
using Common.DTOs.TripVehicleHandoverRecord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface ITripVehicleHandoverRecordService
    {
        Task<bool> CreateTripVehicleHandoverRecordAsync(TripVehicleHandoverRecordCreateDTO dto);
        Task<ResponseDTO> GetByIdAsync(Guid recordId);
        Task<ResponseDTO> SendOtpAsync(Guid recordId);
        Task<ResponseDTO> SignRecordAsync(SignVehicleHandoverDTO dto);

        Task<ResponseDTO> UpdateChecklistAsync(UpdateHandoverChecklistDTO dto);
        Task<ResponseDTO> ReportIssueAsync(ReportHandoverIssueDTO dto);

    }
}
