using Microsoft.Data.SqlClient;
using Microsoft.OpenApi.Models;
using WebApplication_Drone;
using WebApplication_Drone.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

// 添加Aspire服务默认配置
builder.AddServiceDefaults();

#region 基础服务配置

// 路由配置
builder.Services.Configure<RouteOptions>(options =>
{
    options.ConstraintMap.Add("string", typeof(string));
});

// 控制器服务
builder.Services.AddControllers();

// CORS配置
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

#endregion

#region API文档配置

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "AspireApp Drone API", 
        Version = "v1",
        Description = "无人机集群管理系统API",
        Contact = new OpenApiContact 
        { 
            Name = "AspireApp Team",
            Email = "support@aspireapp.com"
        }
    });

    // 文件上传支持
    c.OperationFilter<SwaggerFileOperationFilter>();

    // IFormFile类型参数
    c.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });

    // JWT认证支持（如果需要）
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
});

#endregion

#region 缓存配置

// 内存缓存
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 100; // 限制缓存项数量
    options.CompactionPercentage = 0.1; // 压缩百分比
});

// 分布式缓存（使用内存缓存）
builder.Services.AddDistributedMemoryCache();

#endregion

#region 数据服务配置

// 数据库服务（改为Scoped）
builder.Services.AddScoped<SqlserverService>();

// 业务逻辑服务（改为Scoped，避免内存泄漏和并发问题）
builder.Services.AddScoped<DroneDataService>();
builder.Services.AddScoped<TaskDataService>();

// Socket服务（保持Singleton，因为需要维持长连接状态）
builder.Services.AddSingleton<SocketService>();
builder.Services.AddSingleton<MissionSocketService>();

// 后台服务
builder.Services.AddHostedService<SocketBackgroundService>();

#endregion

#region 健康检查配置

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("API is running"), tags: new[] { "live" })
    .AddSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection") ?? "Server=localhost;Database=AspireApp;Trusted_Connection=true;TrustServerCertificate=true;",
        name: "sqlserver",
        tags: new[] { "ready", "db" })
    .AddCheck<DroneServiceHealthCheck>("drone_service", tags: new[] { "ready", "business" })
    .AddCheck<TaskServiceHealthCheck>("task_service", tags: new[] { "ready", "business" });

// 内存缓存健康检查（简化版本）

#endregion

#region 性能监控配置

// 添加性能计数器
builder.Services.AddSingleton<PerformanceMonitoringService>();
builder.Services.AddHostedService<PerformanceMonitoringService>(provider => 
    provider.GetRequiredService<PerformanceMonitoringService>());

#endregion

#region HTTP客户端配置

// 配置弹性HTTP客户端
builder.Services.AddHttpClient("DefaultApi", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddStandardResilienceHandler(); // 添加重试、熔断、超时处理

#endregion

var app = builder.Build();

// 映射Aspire默认端点（健康检查、指标等）
app.MapDefaultEndpoints();

#region 中间件管道配置

// 开发环境配置
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "AspireApp Drone API v1");
        c.RoutePrefix = string.Empty; // 设置Swagger UI为根路径
        c.DisplayRequestDuration(); // 显示请求耗时
        c.EnableDeepLinking(); // 启用深度链接
    });
}

// HTTPS重定向
app.UseHttpsRedirection();

// 静态文件服务
app.UseStaticFiles();

// CORS
app.UseCors();

// 授权
app.UseAuthorization();

// 控制器路由
app.MapControllers();

#endregion

#region 自定义健康检查端点

// 详细健康检查端点
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                exception = entry.Value.Exception?.Message,
                duration = entry.Value.Duration.ToString(),
                description = entry.Value.Description
            }),
            totalDuration = report.TotalDuration.ToString()
        });
        await context.Response.WriteAsync(result);
    }
});

// 存活检查（用于容器编排）
app.MapHealthChecks("/alive", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

// 就绪检查（用于负载均衡）
app.MapHealthChecks("/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

#endregion

#region 优雅关闭配置

// 注册优雅关闭处理
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    app.Logger.LogInformation("应用程序正在停止...");
});

lifetime.ApplicationStopped.Register(() =>
{
    app.Logger.LogInformation("应用程序已停止");
});

#endregion

app.Logger.LogInformation("🚀 AspireApp Drone API 已启动");
app.Logger.LogInformation("📊 Swagger UI: {SwaggerUrl}", app.Environment.IsDevelopment() ? "https://localhost:5001" : "已禁用");
app.Logger.LogInformation("🏥 健康检查: /health, /alive, /ready");

app.Run();

#region 健康检查实现

/// <summary>
/// 无人机服务健康检查
/// </summary>
public class DroneServiceHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _serviceProvider;

    public DroneServiceHealthCheck(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var droneService = scope.ServiceProvider.GetRequiredService<DroneDataService>();
            
                         // 简单的健康检查：获取无人机数量
             var droneCount = droneService.GetDrones().Count();
            
            return HealthCheckResult.Healthy($"无人机服务正常，当前管理 {droneCount} 架无人机");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("无人机服务异常", ex);
        }
    }
}

/// <summary>
/// 任务服务健康检查
/// </summary>
public class TaskServiceHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _serviceProvider;

    public TaskServiceHealthCheck(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var taskService = scope.ServiceProvider.GetRequiredService<TaskDataService>();
            
                         // 简单的健康检查：获取任务数量
             var taskCount = taskService.GetTasks().Count();
            
            return HealthCheckResult.Healthy($"任务服务正常，当前管理 {taskCount} 个任务");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("任务服务异常", ex);
        }
    }
}

/// <summary>
/// 性能监控服务
/// </summary>
public class PerformanceMonitoringService : BackgroundService
{
    private readonly ILogger<PerformanceMonitoringService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public PerformanceMonitoringService(ILogger<PerformanceMonitoringService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("性能监控服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectPerformanceMetrics();
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // 每5分钟收集一次
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "收集性能指标时发生错误");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("性能监控服务已停止");
    }

    private async Task CollectPerformanceMetrics()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            
            // 收集GC信息
            var gen0 = GC.CollectionCount(0);
            var gen1 = GC.CollectionCount(1);
            var gen2 = GC.CollectionCount(2);
            var totalMemory = GC.GetTotalMemory(false) / 1024 / 1024; // MB

            _logger.LogInformation("性能指标 - GC: Gen0={Gen0}, Gen1={Gen1}, Gen2={Gen2}, 内存={MemoryMB}MB", 
                gen0, gen1, gen2, totalMemory);

            // 收集业务指标
            var droneService = scope.ServiceProvider.GetService<DroneDataService>();
            var taskService = scope.ServiceProvider.GetService<TaskDataService>();

                         if (droneService != null)
             {
                 var droneCount = droneService.GetDrones().Count();
                 _logger.LogInformation("业务指标 - 无人机数量: {DroneCount}", droneCount);
             }

             if (taskService != null)
             {
                 var taskCount = taskService.GetTasks().Count();
                 _logger.LogInformation("业务指标 - 任务数量: {TaskCount}", taskCount);
             }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "收集性能指标时发生错误");
        }
    }
}

#endregion
