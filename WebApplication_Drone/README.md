# WebApplication_Drone - 无人机API服务

## 项目概述

WebApplication_Drone 是无人机集群管理系统的核心API服务，提供无人机管理、任务分发、数据处理和系统监控等功能。基于 ASP.NET Core 8.0 构建，采用现代化的微服务架构设计。

## 🏗️ 项目架构

### 技术栈
- **ASP.NET Core 8.0** - Web API框架
- **SignalR** - 实时通信
- **Entity Framework Core** - 数据访问
- **SQL Server** - 主数据库
- **Redis** - 分布式缓存
- **Swagger/OpenAPI** - API文档
- **Docker** - 容器化支持

### 架构模式
- **分层架构** - Controller → Service → Repository
- **依赖注入** - IoC容器管理
- **异步编程** - 全异步API设计
- **缓存策略** - 多层缓存优化
- **监控体系** - 健康检查和性能监控

## 📁 项目结构

```
WebApplication_Drone/
├── Controllers/              # API控制器
│   ├── DronesController.cs      # 无人机管理API
│   ├── HistoryDataController.cs # 历史数据API
│   ├── SystemController.cs      # 系统监控API
│   └── TasksController.cs       # 任务管理API
├── Services/                 # 业务服务层
│   ├── DroneDataService.cs      # 无人机数据服务
│   ├── TaskDataService.cs       # 任务数据服务
│   ├── SocketService.cs          # Socket通信服务
│   ├── MissionSocketService.cs   # 任务Socket服务
│   └── PerformanceMonitoringService.cs # 性能监控服务
├── Middleware/               # 中间件
│   ├── ExceptionHandlingMiddleware.cs  # 异常处理
│   ├── PerformanceMiddleware.cs         # 性能监控
│   └── RateLimitingMiddleware.cs        # 限流中间件
├── Hubs/                     # SignalR集线器
│   ├── DroneHub.cs              # 无人机实时通信
│   └── TaskHub.cs               # 任务实时通信
├── Models/                   # 数据模型
├── Configuration/            # 配置模型
├── Program.cs               # 程序入口
├── appsettings.json         # 应用配置
└── Dockerfile              # Docker配置
```

## 🚀 核心功能

### 1. 无人机管理API

#### 获取所有无人机
```http
GET /api/drones
```

#### 获取单个无人机详情
```http
GET /api/drones/{id}
```

#### 更新无人机状态
```http
PUT /api/drones/{id}/status
```

#### 获取无人机历史数据
```http
GET /api/drones/{id}/data?startTime={start}&endTime={end}
```

### 2. 任务管理API

#### 获取所有任务
```http
GET /api/tasks
```

#### 创建新任务
```http
POST /api/tasks
Content-Type: application/json

{
  "description": "视频处理任务",
  "priority": 1,
  "videoFile": "base64_encoded_video"
}
```

#### 上传视频任务
```http
POST /api/tasks/upload
Content-Type: multipart/form-data

{
  "Description": "任务描述",
  "VideoFile": "video.mp4",
  "Notes": "备注信息"
}
```

#### 获取任务详情
```http
GET /api/tasks/{id}
```

### 3. 历史数据API

#### 获取系统概览
```http
GET /api/historydata/overview
```

#### 获取任务统计
```http
GET /api/historydata/analysis/task-statistics
```

#### 获取性能分析
```http
GET /api/historydata/analysis/task-performance
```

### 4. 系统监控API

#### 获取系统信息
```http
GET /api/system/info
```

#### 获取性能指标
```http
GET /api/system/current-metrics
```

#### 获取性能历史
```http
GET /api/system/performance-history
```

#### 强制垃圾回收
```http
POST /api/system/force-gc
```

#### 重置性能统计
```http
POST /api/system/reset-stats
```

## ⚙️ 配置管理

### 应用程序配置 (appsettings.json)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=AspireAppDB;Trusted_Connection=true;TrustServerCertificate=true;",
    "cache": "localhost:6379"
  },
  "DroneService": {
    "MaxRetryAttempts": 3,
    "RetryDelay": "00:00:02",
    "CacheExpirationMinutes": 10,
    "EnableRealTimeUpdates": true
  },
  "TaskService": {
    "MaxConcurrentTasks": 50,
    "TaskTimeoutMinutes": 30,
    "AutoRetryFailedTasks": true,
    "MaxRetryAttempts": 3
  },
  "SocketService": {
    "DefaultHost": "192.168.31.35",
    "DefaultPort": 5007,
    "MaxRetries": 5,
    "RetryInterval": "00:00:30",
    "AutoReconnect": true,
    "MaxQueueSize": 1000
  },
  "MissionSocketService": {
    "Host": "192.168.31.35",
    "Port": 5008,
    "MaxRetries": 5,
    "RetryInterval": "00:00:30",
    "AutoReconnect": true
  },
  "Performance": {
    "CollectionInterval": "00:05:00",
    "RetentionHours": 24,
    "CpuThreshold": 80.0,
    "MemoryThreshold": 85.0,
    "EnableAlerts": true
  },
  "RateLimit": {
    "DefaultRequests": 100,
    "DefaultWindow": "00:01:00",
    "WhitelistIPs": ["127.0.0.1", "::1"],
    "EndpointLimits": {
      "/api/tasks/upload": {
        "Requests": 10,
        "Window": "00:01:00"
      }
    }
  },
  "HealthChecks": {
    "UI": {
      "EvaluationTimeInSeconds": 10,
      "MinimumSecondsBetweenFailureNotifications": 60
    }
  }
}
```

## 🔧 服务配置

### 依赖注入配置
```csharp
// 数据库服务（Singleton，确保线程安全的数据库连接管理）
builder.Services.AddSingleton<SqlserverService>();

// 业务逻辑服务（Singleton，保持状态和缓存）
builder.Services.AddSingleton<DroneDataService>();
builder.Services.AddSingleton<TaskDataService>();

// Socket服务（Singleton，维持长连接状态）
builder.Services.AddSingleton<SocketService>();
builder.Services.AddSingleton<MissionSocketService>();

// 性能监控服务
builder.Services.AddSingleton<PerformanceMonitoringService>();

// 后台服务
builder.Services.AddHostedService<SocketBackgroundService>();
```

### 中间件配置
```csharp
// 性能监控中间件
app.UseMiddleware<PerformanceMiddleware>();

// 异常处理中间件
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 限流中间件
app.UseMiddleware<RateLimitingMiddleware>();
```

### 健康检查配置
```csharp
builder.Services.AddHealthChecks()
    .AddCheck<DroneServiceHealthCheck>("drone-service")
    .AddCheck<TaskServiceHealthCheck>("task-service")
    .AddCheck<SocketServiceHealthCheck>("socket-service")
    .AddCheck<NetworkHealthCheck>("network")
    .AddCheck<SystemResourceHealthCheck>("system-resources")
    .AddCheck<DiskSpaceHealthCheck>("disk-space");
```

## 📊 性能监控

### 性能指标收集
- **系统指标**: CPU使用率、内存占用、线程数、句柄数
- **GC指标**: Gen0/Gen1/Gen2回收次数、总分配字节数
- **业务指标**: 无人机数量、任务数量、活跃连接数
- **请求统计**: 每秒请求数、平均响应时间、异常计数

### 性能监控服务
```csharp
public class PerformanceMonitoringService
{
    // 收集系统性能指标
    public async Task<PerformanceMetrics> CollectMetricsAsync()
    {
        return new PerformanceMetrics
        {
            CpuUsage = await GetCpuUsageAsync(),
            MemoryUsage = GetMemoryUsage(),
            ThreadCount = GetThreadCount(),
            HandleCount = GetHandleCount(),
            GcMetrics = GetGcMetrics(),
            BusinessMetrics = GetBusinessMetrics(),
            RequestStats = GetRequestStats()
        };
    }
}
```

### 智能告警
- **CPU/内存阈值告警** - 超过设定阈值时触发
- **GC压力检测** - 频繁GC时发出警告
- **线程数预警** - 线程数过多时提醒
- **异常率监控** - 异常率超标时告警

## 🔒 安全特性

### 限流保护
```csharp
public class RateLimitingMiddleware
{
    // 基于滑动窗口的请求频率控制
    // IP白名单支持
    // 端点级别限制配置
    // 智能清理过期客户端记录
}
```

### 异常处理
```csharp
public class ExceptionHandlingMiddleware
{
    // 统一异常处理
    // 根据异常类型返回相应HTTP状态码
    // 开发环境返回详细异常信息
    // 自动记录异常统计
}
```

## 🚀 部署配置

### Docker 配置
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["WebApplication_Drone/WebApplication_Drone.csproj", "WebApplication_Drone/"]
RUN dotnet restore "./WebApplication_Drone/WebApplication_Drone.csproj"
COPY . .
WORKDIR "/src/WebApplication_Drone"
RUN dotnet build "./WebApplication_Drone.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./WebApplication_Drone.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WebApplication_Drone.dll"]
```

### 环境变量
```bash
# 数据库连接
CONNECTIONSTRINGS__DEFAULTCONNECTION="Server=db;Database=AspireAppDB;User Id=sa;Password=Password123!;"

# Redis连接
CONNECTIONSTRINGS__CACHE="redis:6379"

# 日志级别
LOGGING__LOGLEVEL__DEFAULT="Information"

# 性能监控
PERFORMANCE__ENABLEALERTS="true"
PERFORMANCE__CPUTHRESHOLD="80"
```

## 📈 API文档

### Swagger配置
访问 `/swagger` 查看完整的API文档，包括：
- **请求/响应模型**
- **参数说明**
- **示例代码**
- **错误码说明**

### API版本控制
```csharp
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public class DronesController : ControllerBase
{
    // API实现
}
```

## 🔍 监控端点

### 健康检查
- `/health` - 完整健康状态
- `/health/ready` - 就绪状态检查
- `/health/live` - 存活状态检查

### 指标端点
- `/metrics` - Prometheus格式指标
- `/api/system/info` - 系统信息
- `/api/system/current-metrics` - 当前性能指标

## 🐛 故障排除

### 常见问题

1. **数据库连接失败**
   ```bash
   # 检查连接字符串
   # 验证数据库服务状态
   # 确认网络连接
   ```

2. **Redis连接问题**
   ```bash
   # 检查Redis服务状态
   # 验证连接配置
   # 测试网络连通性
   ```

3. **Socket连接异常**
   ```bash
   # 检查Linux端服务状态
   # 验证IP和端口配置
   # 查看防火墙设置
   ```

### 日志分析
```bash
# 查看应用日志
docker logs aspireapp-drone

# 查看性能日志
grep "Performance" /var/log/aspireapp/app.log

# 查看错误日志
grep "ERROR" /var/log/aspireapp/app.log
```

## 📚 开发指南

### 添加新的API端点
```csharp
[HttpGet("new-endpoint")]
public async Task<IActionResult> NewEndpoint()
{
    try
    {
        var result = await _service.ProcessAsync();
        return Ok(result);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "处理请求时发生错误");
        return StatusCode(500, "内部服务器错误");
    }
}
```

### 添加新的健康检查
```csharp
public class CustomHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // 健康检查逻辑
        return Task.FromResult(HealthCheckResult.Healthy());
    }
}
```

### 性能优化建议
- 使用异步编程模式
- 合理配置缓存策略
- 优化数据库查询
- 监控内存使用情况
- 定期清理临时数据

---

**维护者**: AspireApp 开发团队  
**更新时间**: 2024年12月 