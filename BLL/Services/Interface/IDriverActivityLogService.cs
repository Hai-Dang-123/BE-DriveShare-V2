using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IDriverActivityLogService
    {
        // 1. Get My Logs (Cho tài xế)
        Task<ResponseDTO> GetMyLogsAsync(int page, int pageSize, string logLevel = null);

        // 2. Get Logs By User ID (Cho Admin)
        Task<ResponseDTO> GetLogsByDriverIdAsync(Guid driverId, int page, int pageSize, string logLevel = null);

        // 3. Get All Logs (Cho Admin - Có Search & Filter)
        Task<ResponseDTO> GetAllLogsAsync(int page, int pageSize, string search = null, string logLevel = null);

        // 4. Count/Statistics Log (Cho Admin Dashboard)
        Task<ResponseDTO> GetLogStatisticsAsync(Guid? driverId = null); // Nếu null thì count all, nếu có ID thì count cho user đó
    }
}
