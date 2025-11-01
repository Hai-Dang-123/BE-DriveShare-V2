using BLL.Services.Interface;
using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class UserDocumentService : IUserDocumentService
    {
        public Task<ResponseDTO> CreateAsync(UserDocumentDTO dto)
        {
            throw new NotImplementedException();
        }

        public Task<ResponseDTO> DeleteAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task<ResponseDTO> GetAllAsync()
        {
            throw new NotImplementedException();
        }

        public Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task<ResponseDTO> UpdateAsync(Guid id, UserDocumentDTO dto)
        {
            throw new NotImplementedException();
        }
    }
}
