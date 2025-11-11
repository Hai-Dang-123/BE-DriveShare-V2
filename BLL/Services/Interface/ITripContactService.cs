using Common.DTOs;
using DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface ITripContactService
    {
        Task<ResponseDTO> CreateTripContactAsync(TripContactCreateDTO tripContactDTO);
        Task CopyContactsFromPostAsync(Guid tripId, ICollection<PostContact> postContacts);
    }
}
