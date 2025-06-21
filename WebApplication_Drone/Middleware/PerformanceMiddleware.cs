using System.Diagnostics;
using WebApplication_Drone.Services;

namespace WebApplication_Drone.Middleware
{
    /// <summary>
    /// 性能监控中间件
    /// </summary>
    public class PerformanceMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<PerformanceMiddleware> _logger;
        private readonly PerformanceMonitoringService? _performanceService;

        public PerformanceMiddleware(
            RequestDelegate next, 
            ILogger<PerformanceMiddleware> logger,
            IServiceProvider serviceProvider)
        {
            _next = next;
            _logger = logger;
            
            // 尝试获取性能监控服务（可能未注册）
            try
            {
                _performanceService = serviceProvider.GetService<PerformanceMonitoringService>();
            }
            catch
            {
                _performanceService = null;
            }
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var startTime = DateTime.UtcNow;

            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                var responseTime = stopwatch.ElapsedMilliseconds;

                // 记录性能指标
                _performanceService?.RecordRequest(responseTime);

                // 记录慢请求
                if (responseTime > 1000) // 超过1秒的请求
                {
                    _logger.LogWarning("慢请求检测: {Method} {Path} 耗时 {ResponseTime}ms, 状态码: {StatusCode}",
                        context.Request.Method,
                        context.Request.Path,
                        responseTime,
                        context.Response.StatusCode);
                }
                else if (responseTime > 500) // 超过500ms的请求
                {
                    _logger.LogInformation("请求性能: {Method} {Path} 耗时 {ResponseTime}ms, 状态码: {StatusCode}",
                        context.Request.Method,
                        context.Request.Path,
                        responseTime,
                        context.Response.StatusCode);
                }

                // 添加响应头（仅在响应未开始时）
                if (!context.Response.HasStarted)
                {
                    context.Response.Headers.TryAdd("X-Response-Time", $"{responseTime}ms");
                    context.Response.Headers.TryAdd("X-Timestamp", startTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
                }
            }
        }
    }

    /// <summary>
    /// 性能中间件扩展方法
    /// </summary>
    public static class PerformanceMiddlewareExtensions
    {
        public static IApplicationBuilder UsePerformanceMonitoring(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<PerformanceMiddleware>();
        }
    }
} 