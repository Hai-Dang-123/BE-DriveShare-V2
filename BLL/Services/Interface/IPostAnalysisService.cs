using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IPostAnalysisService
    {
        Task<ResponseDTO> GetOrGeneratePackageAnalysisAsync(Guid postPackageId);
        Task<ResponseDTO> GetOrGenerateTripAnalysisAsync(Guid postTripId);
    }
}
