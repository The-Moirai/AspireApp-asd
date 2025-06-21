using System.Net;
using System.Text.Json;
using WebApplication_Drone.Services;

namespace WebApplication_Drone.Middleware
{
    /// <summary>
    /// 异常响应模型
    /// </summary>
    public class ErrorResponse
    {
        public string Message { get; set; } = "";
        public string? Detail { get; set; }
        public string? TraceId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Path { get; set; } = "";
        public int StatusCode { get; set; }
    }

    /// <summary>
    /// 全局异常处理中间件
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly PerformanceMonitoringService? _performanceService;

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger,
            IWebHostEnvironment environment,
            IServiceProvider serviceProvider)
        {
            _next = next;
            _logger = logger;
            _environment = environment;

            // 尝试获取性能监控服务
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
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                // 记录异常统计
                _performanceService?.RecordException();

                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var traceId = context.TraceIdentifier;
            var path = context.Request.Path.ToString();

            // 根据异常类型确定状态码和消息
            var (statusCode, message) = GetErrorDetails(exception);

            // 记录详细日志
            _logger.LogError(exception,
                "未处理的异常发生 - TraceId: {TraceId}, Path: {Path}, StatusCode: {StatusCode}, Message: {Message}",
                traceId, path, statusCode, message);

            // 构建错误响应
            var errorResponse = new ErrorResponse
            {
                Message = message,
                TraceId = traceId,
                Path = path,
                StatusCode = statusCode,
                Detail = _environment.IsDevelopment() ? exception.ToString() : null
            };

            // 设置响应（仅在响应未开始时）
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/json";
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = _environment.IsDevelopment()
            };

            // 仅在响应未开始时写入错误信息
            if (!context.Response.HasStarted)
            {
                var jsonResponse = JsonSerializer.Serialize(errorResponse, jsonOptions);
                await context.Response.WriteAsync(jsonResponse);
            }
        }

        private static (int statusCode, string message) GetErrorDetails(Exception exception)
        {
            return exception switch
            {
                ArgumentException => (400, "请求参数无效"),
                InvalidOperationException => (400, "操作无效"),
                UnauthorizedAccessException => (401, "未授权访问"),
                NotImplementedException => (501, "功能未实现"),
                TimeoutException => (408, "请求超时"),
                TaskCanceledException => (408, "请求被取消"),
                HttpRequestException => (500, "HTTP请求异常"),
                FileNotFoundException => (404, "文件未找到"),
                DirectoryNotFoundException => (404, "目录未找到"),
                OutOfMemoryException => (503, "服务器内存不足"),
                StackOverflowException => (503, "服务器资源不足"),
                _ => (500, "服务器内部错误")
            };
        }
    }

    /// <summary>
    /// 异常处理中间件扩展方法
    /// </summary>
    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
} 