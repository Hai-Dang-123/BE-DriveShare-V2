using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BLL.Utilities
{
    public class CustomFormFile : IFormFile
    {
        private readonly Stream _stream;
        private readonly string _name;
        private readonly string _fileName;
        private readonly string _contentType;

        public CustomFormFile(Stream stream, string name, string fileName, string contentType)
        {
            _stream = stream;
            _name = name;
            _fileName = fileName;
            _contentType = contentType;
        }

        public string ContentType => _contentType;
        public string ContentDisposition => $"form-data; name=\"{_name}\"; filename=\"{_fileName}\"";
        public IHeaderDictionary Headers => new HeaderDictionary();
        public long Length => _stream.Length;
        public string Name => _name;
        public string FileName => _fileName;

        public void CopyTo(Stream target)
        {
            _stream.Position = 0;
            _stream.CopyTo(target);
        }

        public async Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        {
            _stream.Position = 0;
            await _stream.CopyToAsync(target, cancellationToken);
        }

        public Stream OpenReadStream()
        {
            // Quan trọng: Reset position về 0 trước khi trả về
            _stream.Position = 0;
            return _stream;
        }
    }
}