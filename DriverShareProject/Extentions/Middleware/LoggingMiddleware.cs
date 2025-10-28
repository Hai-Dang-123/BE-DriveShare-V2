using System.Diagnostics;

namespace DriverShareProject.Extentions.Middleware
{
    public class LoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<LoggingMiddleware> _logger;

        public LoggingMiddleware(RequestDelegate next, ILogger<LoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();

            var request = context.Request;
            var method = request.Method;
            var path = request.Path + request.QueryString;

            _logger.LogInformation("➡️ [Request] {Method} {Path}", method, path);

            // Ghi tạm response vào memory stream để log rồi trả lại
            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            await _next(context);

            stopwatch.Stop();

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseText = await new StreamReader(context.Response.Body).ReadToEndAsync();
            context.Response.Body.Seek(0, SeekOrigin.Begin);

            var statusCode = context.Response.StatusCode;
            var truncatedResponse = responseText.Length > 500 ? responseText.Substring(0, 500) + "..." : responseText;

            _logger.LogInformation("⬅️ [Response] {StatusCode} - {ResponseBody} ({Elapsed}ms)",
                statusCode,
                truncatedResponse,
                stopwatch.ElapsedMilliseconds);

            await responseBody.CopyToAsync(originalBodyStream);
        }
    }
}
