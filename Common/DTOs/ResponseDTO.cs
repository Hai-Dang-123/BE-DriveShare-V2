using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class ResponseDTO
    {
        public int StatusCode { get; set; }
        public string? Message { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public object? Result { get; set; }




        public override string ToString() => JsonSerializer.Serialize(this);

        public ResponseDTO() { }
        public ResponseDTO(string message = "", int statusCode = 200, bool success = true, object? result = null)
        {
            Message = message;
            StatusCode = statusCode;
            IsSuccess = success;
            Result = result;

        }
    }
}
