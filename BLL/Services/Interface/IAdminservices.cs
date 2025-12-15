using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IAdminservices 
    {
        Task<ResponseDTO> GetOverview();
        Task<ResponseDTO> GetUserCountByRole();
        Task<ResponseDTO> GetUserRegistrationStats(DateTime from, DateTime to, string groupBy);
        Task<ResponseDTO> GetTripStatsByStatus();
        Task<ResponseDTO> GetTripCreatedStats(DateTime from, DateTime to, string groupBy);
        Task<ResponseDTO> GetPackageStatsByStatus();
        Task<ResponseDTO> GetPackageCreatedStats(DateTime from, DateTime to, string groupBy);
        Task<ResponseDTO> GetRevenueStats(DateTime from, DateTime to, string groupBy);
    }
}
