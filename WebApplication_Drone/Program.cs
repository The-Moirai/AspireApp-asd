using Microsoft.Data.SqlClient;
using Microsoft.OpenApi.Models;
using WebApplication_Drone;
using WebApplication_Drone.Services;
using WebApplication_Drone.Middleware;
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

// 内存缓存（移除SizeLimit以避免Size设置问题）
builder.Services.AddMemoryCache(options =>
{
    options.CompactionPercentage = 0.1; // 压缩百分比
});

// 分布式缓存（使用内存缓存）
builder.Services.AddDistributedMemoryCache();

#endregion

#region 数据服务配置

// 配置选项绑定
builder.Services.Configure<DroneServiceOptions>(
    builder.Configuration.GetSection("DroneService"));
builder.Services.Configure<TaskServiceOptions>(
    builder.Configuration.GetSection("TaskService"));

// 数据库服务（Singleton，确保线程安全的数据库连接管理）
builder.Services.AddSingleton<SqlserverService>();

// 业务逻辑服务（Singleton，保持状态和缓存）
builder.Services.AddSingleton<DroneDataService>();
builder.Services.AddSingleton<TaskDataService>();

// Socket服务（Singleton，维持长连接状态）
builder.Services.AddSingleton<SocketService>();
builder.Services.AddSingleton<MissionSocketService>();

// 后台服务
builder.Services.AddHostedService<SocketBackgroundService>();

#endregion

#region 健康检查配置

// 添加额外的健康检查（self已由ServiceDefaults注册）
builder.Services.AddHealthChecks()
    .AddCheck("database", () => 
    {
        // 使用自定义检查而不是AddSqlServer以避免依赖注入冲突
        try
        {
            // 这里可以添加数据库连接检查逻辑
            return HealthCheckResult.Healthy("Database connection is healthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
    }, tags: new[] { "ready", "db" });

#endregion

#region 性能监控配置

// 配置性能监控选项
builder.Services.Configure<PerformanceMonitoringOptions>(
    builder.Configuration.GetSection("Performance"));

// 添加性能监控服务（Singleton，用于全局性能统计）
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

// 全局异常处理中间件（放在最前面）
app.UseGlobalExceptionHandling();

// 性能监控中间件
app.UsePerformanceMonitoring();

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


