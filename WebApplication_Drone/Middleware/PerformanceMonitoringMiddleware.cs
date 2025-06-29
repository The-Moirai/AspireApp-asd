using WebApplication_Drone.Services;
using System.Diagnostics;

namespace WebApplication_Drone.Middleware
{
    /// <summary>
    /// 性能监控中间件 - 自动收集请求性能数据
    /// </summary>
    public class PerformanceMonitoringMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<PerformanceMonitoringMiddleware> _logger;
        private readonly PerformanceMonitoringService _performanceService;

        public PerformanceMonitoringMiddleware(
            RequestDelegate next,
            ILogger<PerformanceMonitoringMiddleware> logger,
            PerformanceMonitoringService performanceService)
        {
            _next = next;
            _logger = logger;
            _performanceService = performanceService;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var startTime = DateTime.UtcNow;
            var originalBodyStream = context.Response.Body;

            try
            {
                // 记录请求开始
                _performanceService.RecordRequestStart();

                // 处理请求
                await _next(context);

                // 记录请求完成
                stopwatch.Stop();
                _performanceService.RecordRequestComplete(stopwatch.ElapsedMilliseconds);

                // 记录响应状态
                if (context.Response.StatusCode >= 400)
                {
                    _performanceService.RecordException();
                    _logger.LogWarning("请求返回错误状态码: {StatusCode} - {Path}", 
                        context.Response.StatusCode, context.Request.Path);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _performanceService.RecordException();
                
                _logger.LogError(ex, "请求处理异常: {Path}", context.Request.Path);
                
                // 重新抛出异常，让其他中间件处理
                throw;
            }
            finally
            {
                // 记录请求详情（可选，用于调试）
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("请求处理完成: {Method} {Path} - {StatusCode} - {ElapsedMs}ms",
                        context.Request.Method,
                        context.Request.Path,
                        context.Response.StatusCode,
                        stopwatch.ElapsedMilliseconds);
                }
            }
        }
    }

    /// <summary>
    /// 性能监控中间件扩展方法
    /// </summary>
    public static class PerformanceMonitoringMiddlewareExtensions
    {
        public static IApplicationBuilder UsePerformanceMonitoring(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<PerformanceMonitoringMiddleware>();
        }
    }
} 