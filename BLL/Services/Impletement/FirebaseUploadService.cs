using BLL.Services.Interface;
using Common.Settings;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class FirebaseUploadService : IFirebaseUploadService
    {
        private readonly FirebaseSetting _firebaseSetting;
        private readonly StorageClient _storageClient;

        public FirebaseUploadService(IOptions<FirebaseSetting> firebaseSetting)
        {
            _firebaseSetting = firebaseSetting.Value;

            var credentialPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "firebase",
                _firebaseSetting.TokenPath);

            var credential = GoogleCredential.FromFile(credentialPath);




            _storageClient = StorageClient.Create(credential);
        }

        public async Task<string> UploadFileAsync(IFormFile file, Guid userId, FirebaseFileType fileType)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File không hợp lệ");

            var bucketName = _firebaseSetting.BucketName;

            // test hoặc production folder
            var baseFolder = _firebaseSetting.IsProduction ? "production/users" : "dev-test/users";

            var objectPath = $"{baseFolder}/{userId}/{fileType}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

            using (var stream = file.OpenReadStream())
            {
                await _storageClient.UploadObjectAsync(
                    bucketName,
                    objectPath,
                    null,
                    stream,
                    new UploadObjectOptions
                    {
                        PredefinedAcl = PredefinedObjectAcl.PublicRead
                    });
            }

            return $"https://firebasestorage.googleapis.com/v0/b/{bucketName}/o/{Uri.EscapeDataString(objectPath)}?alt=media";

        }
    }
}
