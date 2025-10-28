using Common.Settings;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IFirebaseUploadService
    {
        Task<string> UploadFileAsync(IFormFile file, Guid userId, FirebaseFileType fileType);
    }
}
