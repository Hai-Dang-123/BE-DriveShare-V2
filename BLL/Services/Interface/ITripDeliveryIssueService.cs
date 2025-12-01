using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface ITripDeliveryIssueService
    {
        Task<ResponseDTO> ReportIssueAsync(TripDeliveryIssueCreateDTO dto);
        Task<ResponseDTO> GetIssuesByTripIdAsync(Guid tripId);
        Task<ResponseDTO> GetIssueByIdAsync(Guid issueId);
        Task<ResponseDTO> UpdateIssueStatusAsync(UpdateIssueStatusDTO dto);
    }
}
