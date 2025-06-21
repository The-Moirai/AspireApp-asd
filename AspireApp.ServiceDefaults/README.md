# AspireApp.ServiceDefaults - 共享服务配置

## 项目概述

AspireApp.ServiceDefaults 是 .NET Aspire 应用程序的共享服务配置库，为整个分布式系统提供统一的服务注册、配置管理、健康检查、遥测和中间件配置。所有微服务都引用此项目以确保一致的基础设施配置。

## 🏗️ 项目架构

### 技术栈
- **.NET 8.0** - 目标框架
- **.NET Aspire 9.3.1** - 云原生框架
- **OpenTelemetry** - 可观测性
- **Microsoft.Extensions.ServiceDiscovery** - 服务发现
- **Microsoft.Extensions.Http.Resilience** - HTTP弹性

### 核心职责
- **服务默认配置** - 统一的服务注册和配置
- **可观测性配置** - 日志、指标、追踪的统一设置
- **健康检查配置** - 标准化的健康检查端点
- **HTTP客户端配置** - 弹性和服务发现配置
- **异常处理** - 全局异常处理机制

## 📁 项目结构

```
AspireApp.ServiceDefaults/
├── Extensions.cs                    # 核心扩展方法
├── AspireApp.ServiceDefaults.csproj # 项目文件
├── bin/                            # 编译输出
└── obj/                            # 编译临时文件
```

## 🚀 核心功能

### 1. 服务默认配置 (AddServiceDefaults)

#### 主要功能
```csharp
public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) 
    where TBuilder : IHostApplicationBuilder
{
    // 配置 OpenTelemetry
    builder.ConfigureOpenTelemetry();

    // 添加默认健康检查
    builder.AddDefaultHealthChecks();

    // 添加服务发现
    builder.Services.AddServiceDiscovery();

    // 配置 HTTP 客户端默认设置
    builder.Services.ConfigureHttpClientDefaults(http =>
    {
        // 启用弹性处理
        http.AddStandardResilienceHandler();
        
        // 启用服务发现
        http.AddServiceDiscovery();
    });

    // 添加全局异常处理
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    // 添加内存缓存
    builder.Services.AddMemoryCache();
    
    // 添加数据保护
    builder.Services.AddDataProtection();

    return builder;
}
```

#### 服务发现配置
- **自动服务注册** - 服务启动时自动注册到服务注册中心
- **服务解析** - 自动解析服务依赖关系
- **负载均衡** - 内置负载均衡策略
- **健康检查集成** - 与健康检查系统集成

### 2. OpenTelemetry 配置

#### 日志配置
```csharp
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});
```

#### 指标配置
```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddRuntimeInstrumentation();
    });
```

#### 追踪配置
```csharp
.WithTracing(tracing =>
{
    tracing.AddAspNetCoreInstrumentation()
           .AddHttpClientInstrumentation();
});
```

### 3. 健康检查配置

#### 默认健康检查
```csharp
public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) 
    where TBuilder : IHostApplicationBuilder
{
    builder.Services.AddHealthChecks()
        // 基础自检
        .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

    return builder;
}
```

#### 健康检查端点映射
```csharp
public static WebApplication MapDefaultEndpoints(this WebApplication app)
{
    // 健康检查端点
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/alive", new HealthCheckOptions
    {
        Predicate = r => r.Tags.Contains("live")
    });

    return app;
}
```

### 4. 业务服务配置扩展

#### 业务服务注册
```csharp
public static TBuilder AddBusinessServices<TBuilder>(this TBuilder builder) 
    where TBuilder : IHostApplicationBuilder
{
    // 配置选项模式
    builder.Services.Configure<DroneServiceOptions>(
        builder.Configuration.GetSection("DroneService"));
    builder.Services.Configure<SocketServiceOptions>(
        builder.Configuration.GetSection("SocketService"));
    builder.Services.Configure<DatabaseOptions>(
        builder.Configuration.GetSection("Database"));

    // 添加验证
    builder.Services.AddOptionsWithValidateOnStart<DroneServiceOptions>()
        .BindConfiguration("DroneService")
        .ValidateDataAnnotations();

    return builder;
}
```

#### 分布式缓存配置
```csharp
public static TBuilder AddDistributedCaching<TBuilder>(this TBuilder builder) 
    where TBuilder : IHostApplicationBuilder
{
    // Redis分布式缓存
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration.GetConnectionString("cache");
        options.InstanceName = "AspireApp";
    });

    // 添加缓存服务
    builder.Services.AddScoped<ICacheService, RedisCacheService>();

    return builder;
}
```

### 5. 全局异常处理

#### 异常处理器实现
```csharp
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "An unhandled exception occurred");

        var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An error occurred",
            Detail = httpContext.RequestServices
                .GetRequiredService<IHostEnvironment>().IsDevelopment() 
                ? exception.Message 
                : "An internal server error occurred"
        };

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
```

## ⚙️ 配置选项类

### 无人机服务配置
```csharp
public class DroneServiceOptions
{
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);
    public int CacheExpirationMinutes { get; set; } = 10;
    public bool EnableRealTimeUpdates { get; set; } = true;
}
```

### Socket服务配置
```csharp
public class SocketServiceOptions
{
    public string DefaultHost { get; set; } = "192.168.31.35";
    public int DefaultPort { get; set; } = 5007;
    public int MaxRetries { get; set; } = 5;
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(30);
    public bool AutoReconnect { get; set; } = true;
    public int MaxQueueSize { get; set; } = 1000;
}
```

### 数据库配置
```csharp
public class DatabaseOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public int CommandTimeout { get; set; } = 30;
    public int MaxRetryCount { get; set; } = 3;
    public bool EnableSensitiveDataLogging { get; set; } = false;
}
```

## 🔧 项目配置

### 项目文件 (AspireApp.ServiceDefaults.csproj)
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsAspireSharedProject>true</IsAspireSharedProject>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />

    <PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="9.6.0" />
    <PackageReference Include="Microsoft.Extensions.ServiceDiscovery" Version="9.3.1" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.12.0" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.12.0" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.12.0" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.12.0" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.12.0" />
  </ItemGroup>
</Project>
```

### 项目特性
- **IsAspireSharedProject** - 标识为 Aspire 共享项目
- **FrameworkReference** - 引用 ASP.NET Core 框架
- **OpenTelemetry 包** - 完整的可观测性支持
- **服务发现包** - 微服务发现能力
- **HTTP弹性包** - HTTP客户端弹性处理

## 📊 可观测性配置

### 指标收集
- **ASP.NET Core 指标** - 请求量、响应时间、错误率
- **HTTP 客户端指标** - 出站请求统计
- **运行时指标** - GC、内存、线程池统计
- **自定义业务指标** - 业务相关的关键指标

### 日志配置
- **结构化日志** - JSON格式的结构化日志输出
- **作用域支持** - 日志作用域和上下文信息
- **格式化消息** - 包含格式化的日志消息
- **OpenTelemetry 集成** - 与追踪系统集成

### 分布式追踪
- **请求追踪** - 完整的请求生命周期追踪
- **跨服务追踪** - 微服务间的调用链追踪
- **性能分析** - 请求性能瓶颈识别
- **错误追踪** - 异常和错误的上下文信息

## 🔒 安全和弹性

### HTTP 弹性配置
- **重试策略** - 自动重试失败的请求
- **断路器** - 防止级联故障
- **超时控制** - 请求超时保护
- **限流保护** - 防止服务过载

### 数据保护
- **密钥管理** - 自动密钥生成和轮换
- **数据加密** - 敏感数据加密存储
- **跨应用保护** - 多应用间的数据保护
- **持久化支持** - 密钥持久化存储

## 📚 使用示例

### 在微服务中使用
```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// 添加默认服务配置
builder.AddServiceDefaults();

// 添加业务服务配置
builder.AddBusinessServices();

// 添加分布式缓存
builder.AddDistributedCaching();

var app = builder.Build();

// 映射默认端点
app.MapDefaultEndpoints();

app.Run();
```

### 自定义健康检查
```csharp
// 添加自定义健康检查
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<ExternalServiceHealthCheck>("external-api");
```

### 配置选项使用
```csharp
// 注入配置选项
public class DroneService
{
    private readonly DroneServiceOptions _options;
    
    public DroneService(IOptions<DroneServiceOptions> options)
    {
        _options = options.Value;
    }
    
    public async Task ProcessAsync()
    {
        // 使用配置
        var maxRetries = _options.MaxRetryAttempts;
        var delay = _options.RetryDelay;
    }
}
```

## 🔧 扩展和自定义

### 添加新的服务配置
```csharp
public static TBuilder AddCustomService<TBuilder>(this TBuilder builder) 
    where TBuilder : IHostApplicationBuilder
{
    // 添加自定义服务配置
    builder.Services.Configure<CustomServiceOptions>(
        builder.Configuration.GetSection("CustomService"));
    
    // 注册服务
    builder.Services.AddScoped<ICustomService, CustomService>();
    
    return builder;
}
```

### 自定义异常处理
```csharp
public class CustomExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // 自定义异常处理逻辑
        return await Task.FromResult(true);
    }
}
```

### 扩展健康检查
```csharp
public static TBuilder AddAdvancedHealthChecks<TBuilder>(this TBuilder builder) 
    where TBuilder : IHostApplicationBuilder
{
    builder.Services.AddHealthChecks()
        .AddCheck<MemoryHealthCheck>("memory")
        .AddCheck<DiskSpaceHealthCheck>("disk")
        .AddCheck<NetworkHealthCheck>("network");
    
    return builder;
}
```

## 🔍 最佳实践

### 配置管理
- 使用强类型配置选项
- 启用配置验证
- 分层配置结构
- 环境特定配置

### 可观测性
- 统一日志格式
- 关键指标监控
- 分布式追踪启用
- 性能基线建立

### 错误处理
- 全局异常处理
- 结构化错误响应
- 敏感信息保护
- 错误分类和统计

### 服务发现
- 健康检查集成
- 负载均衡配置
- 服务版本管理
- 故障转移策略

---

**维护者**: AspireApp 开发团队  
**更新时间**: 2024年12月 