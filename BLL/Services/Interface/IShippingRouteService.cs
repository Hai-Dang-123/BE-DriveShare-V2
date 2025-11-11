using Common.DTOs;
using DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IShippingRouteService
    {
        Task<ResponseDTO> CreateShippingRouteAsync(CreateShippingRouteDTO dto);

        Task<ShippingRoute> CreateAndAddShippingRouteAsync(ShippingRouteInputDTO dto);
    }
}
