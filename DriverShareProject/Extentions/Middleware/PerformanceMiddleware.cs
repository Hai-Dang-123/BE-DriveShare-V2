using Common.Settings;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace DriverShareProject.Extentions.Middleware
{
    public class PerformanceMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<PerformanceMiddleware> _logger;
        private readonly long _warningThresholdMs;

        public PerformanceMiddleware(RequestDelegate next,
                                     ILogger<PerformanceMiddleware> logger,
                                     IOptions<PerformanceSetting> options)
        {
            _next = next;
            _logger = logger;
            _warningThresholdMs = options.Value.WarningThresholdMs;
        }

        public async Task Invoke(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            await _next(context);
            stopwatch.Stop();

            var elapsedMs = stopwatch.ElapsedMilliseconds;
            var method = context.Request.Method;
            var path = context.Request.Path + context.Request.QueryString;

            if (elapsedMs > _warningThresholdMs)
            {
                _logger.LogWarning("⚠️ [Slow Request] {Method} {Path} took {Elapsed} ms", method, path, elapsedMs);
            }
            else
            {
                _logger.LogInformation("⏱️ [Performance] {Method} {Path} executed in {Elapsed} ms", method, path, elapsedMs);
            }
        }
    }
}
