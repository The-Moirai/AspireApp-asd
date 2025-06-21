using System.Collections.Concurrent;
using System.Net;

namespace WebApplication_Drone.Middleware
{
    /// <summary>
    /// 限流配置选项
    /// </summary>
    public class RateLimitOptions
    {
        public int RequestLimit { get; set; } = 100; // 每个时间窗口允许的请求数
        public TimeSpan TimeWindow { get; set; } = TimeSpan.FromMinutes(1); // 时间窗口
        public bool EnableRateLimiting { get; set; } = false; // 是否启用限流
        public string[] WhitelistedIPs { get; set; } = Array.Empty<string>(); // IP白名单
        public Dictionary<string, int> EndpointLimits { get; set; } = new(); // 特定端点的限制
    }

    /// <summary>
    /// 客户端请求记录
    /// </summary>
    public class ClientRequestInfo
    {
        public int RequestCount { get; set; }
        public DateTime WindowStart { get; set; }
        public DateTime LastRequest { get; set; }
    }

    /// <summary>
    /// API限流中间件
    /// </summary>
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitingMiddleware> _logger;
        private readonly RateLimitOptions _options;
        private readonly ConcurrentDictionary<string, ClientRequestInfo> _clients = new();
        private readonly Timer _cleanupTimer;

        public RateLimitingMiddleware(
            RequestDelegate next,
            ILogger<RateLimitingMiddleware> logger,
            IConfiguration configuration)
        {
            _next = next;
            _logger = logger;
            
            // 从配置读取限流设置
            _options = new RateLimitOptions();
            configuration.GetSection("RateLimit").Bind(_options);

            // 启动清理定时器，每分钟清理过期的客户端记录
            _cleanupTimer = new Timer(CleanupExpiredClients, null, 
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!_options.EnableRateLimiting)
            {
                await _next(context);
                return;
            }

            var clientId = GetClientIdentifier(context);
            var endpoint = GetEndpointKey(context);

            // 检查IP白名单
            if (IsWhitelisted(clientId))
            {
                await _next(context);
                return;
            }

            // 获取该端点的限制
            var limit = GetEndpointLimit(endpoint);

            // 检查是否超过限制
            if (IsRateLimited(clientId, limit))
            {
                await HandleRateLimitExceeded(context, clientId, endpoint);
                return;
            }

            // 记录请求
            RecordRequest(clientId);

            await _next(context);
        }

        /// <summary>
        /// 获取客户端标识符
        /// </summary>
        private string GetClientIdentifier(HttpContext context)
        {
            // 优先使用X-Forwarded-For头（代理环境）
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            // 使用真实IP
            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
            {
                return realIp;
            }

            // 使用连接的远程IP
            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        /// <summary>
        /// 获取端点键值
        /// </summary>
        private string GetEndpointKey(HttpContext context)
        {
            return $"{context.Request.Method}:{context.Request.Path}";
        }

        /// <summary>
        /// 检查IP是否在白名单中
        /// </summary>
        private bool IsWhitelisted(string clientId)
        {
            return _options.WhitelistedIPs.Contains(clientId) ||
                   clientId == "127.0.0.1" || clientId == "::1"; // 本地IP始终在白名单
        }

        /// <summary>
        /// 获取端点限制
        /// </summary>
        private int GetEndpointLimit(string endpoint)
        {
            if (_options.EndpointLimits.TryGetValue(endpoint, out var limit))
            {
                return limit;
            }

            // 特殊端点的默认限制
            if (endpoint.Contains("/health") || endpoint.Contains("/alive") || endpoint.Contains("/ready"))
            {
                return _options.RequestLimit * 5; // 健康检查端点更宽松
            }

            if (endpoint.Contains("/api/system"))
            {
                return _options.RequestLimit / 2; // 系统API更严格
            }

            return _options.RequestLimit;
        }

        /// <summary>
        /// 检查是否被限流
        /// </summary>
        private bool IsRateLimited(string clientId, int limit)
        {
            var now = DateTime.UtcNow;
            var clientInfo = _clients.GetOrAdd(clientId, _ => new ClientRequestInfo
            {
                WindowStart = now,
                RequestCount = 0,
                LastRequest = now
            });

            lock (clientInfo)
            {
                // 检查是否需要重置时间窗口
                if (now - clientInfo.WindowStart >= _options.TimeWindow)
                {
                    clientInfo.WindowStart = now;
                    clientInfo.RequestCount = 0;
                }

                // 检查是否超过限制
                return clientInfo.RequestCount >= limit;
            }
        }

        /// <summary>
        /// 记录请求
        /// </summary>
        private void RecordRequest(string clientId)
        {
            if (_clients.TryGetValue(clientId, out var clientInfo))
            {
                lock (clientInfo)
                {
                    clientInfo.RequestCount++;
                    clientInfo.LastRequest = DateTime.UtcNow;
                }
            }
        }

        /// <summary>
        /// 处理限流超出
        /// </summary>
        private async Task HandleRateLimitExceeded(HttpContext context, string clientId, string endpoint)
        {
            var resetTime = DateTime.UtcNow.Add(_options.TimeWindow);
            
            _logger.LogWarning("客户端 {ClientId} 访问 {Endpoint} 超出限流限制", clientId, endpoint);

            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.ContentType = "application/json";

            // 添加限流相关的响应头
            context.Response.Headers.TryAdd("X-RateLimit-Limit", _options.RequestLimit.ToString());
            context.Response.Headers.TryAdd("X-RateLimit-Remaining", "0");
            context.Response.Headers.TryAdd("X-RateLimit-Reset", ((DateTimeOffset)resetTime).ToUnixTimeSeconds().ToString());
            context.Response.Headers.TryAdd("Retry-After", ((int)_options.TimeWindow.TotalSeconds).ToString());

            var response = new
            {
                error = "Rate limit exceeded",
                message = $"请求过于频繁，请在 {_options.TimeWindow.TotalSeconds} 秒后重试",
                resetTime = resetTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                limit = _options.RequestLimit,
                timeWindow = _options.TimeWindow.TotalSeconds
            };

            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
        }

        /// <summary>
        /// 清理过期的客户端记录
        /// </summary>
        private void CleanupExpiredClients(object? state)
        {
            try
            {
                var now = DateTime.UtcNow;
                var expiredClients = new List<string>();

                foreach (var kvp in _clients)
                {
                    var clientInfo = kvp.Value;
                    lock (clientInfo)
                    {
                        // 如果客户端超过2个时间窗口没有请求，则清理
                        if (now - clientInfo.LastRequest > _options.TimeWindow.Add(_options.TimeWindow))
                        {
                            expiredClients.Add(kvp.Key);
                        }
                    }
                }

                foreach (var clientId in expiredClients)
                {
                    _clients.TryRemove(clientId, out _);
                }

                if (expiredClients.Count > 0)
                {
                    _logger.LogDebug("清理了 {Count} 个过期的客户端记录", expiredClients.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理过期客户端记录时发生错误");
            }
        }

        /// <summary>
        /// 获取当前限流统计
        /// </summary>
        public Dictionary<string, object> GetRateLimitStats()
        {
            var stats = new Dictionary<string, object>
            {
                ["TotalClients"] = _clients.Count,
                ["IsEnabled"] = _options.EnableRateLimiting,
                ["RequestLimit"] = _options.RequestLimit,
                ["TimeWindowSeconds"] = _options.TimeWindow.TotalSeconds,
                ["WhitelistedIPCount"] = _options.WhitelistedIPs.Length
            };

            // 统计活跃客户端
            var now = DateTime.UtcNow;
            var activeClients = 0;
            var totalRequests = 0;

            foreach (var kvp in _clients)
            {
                var clientInfo = kvp.Value;
                lock (clientInfo)
                {
                    if (now - clientInfo.LastRequest < _options.TimeWindow)
                    {
                        activeClients++;
                        totalRequests += clientInfo.RequestCount;
                    }
                }
            }

            stats["ActiveClients"] = activeClients;
            stats["TotalRequestsInCurrentWindow"] = totalRequests;

            return stats;
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        }
    }

    /// <summary>
    /// 限流中间件扩展方法
    /// </summary>
    public static class RateLimitingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RateLimitingMiddleware>();
        }
    }
} 