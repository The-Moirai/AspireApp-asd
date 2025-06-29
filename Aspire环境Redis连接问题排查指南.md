# Aspire环境Redis连接问题排查指南

## 概述

在Aspire环境中，Redis服务是通过容器编排运行的，与传统的独立Redis服务有所不同。本指南专门针对Aspire环境中的Redis连接问题进行排查和解决。

## Aspire环境特点

### 1. 服务发现机制
- Redis服务通过`builder.AddRedis("cache")`注册
- 服务名"cache"只在Aspire内部网络可解析
- 连接字符串通过Aspire配置自动注入

### 2. 网络架构
```
Aspire AppHost
├── WebApplication_Drone (API服务)
├── BlazorApp_Web (前端应用)
└── Redis Container (cache服务)
```

### 3. 配置方式
```csharp
// AspireApp.AppHost/Program.cs
var cache = builder.AddRedis("cache");

// WebApplication_Drone/Program.cs
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("cache");
});
```

## 常见问题及解决方案

### 问题1: 网络连接测试失败

**症状**: 诊断结果显示"网络连接测试失败: 不知道这样的主机"

**原因**: 在Aspire环境中，服务名"cache"不能通过标准TCP连接直接访问

**解决方案**:
1. 修改网络连接测试逻辑，使用Redis协议而非TCP连接
2. 在诊断服务中跳过TCP连接测试，直接通过Redis连接验证

**修复代码**:
```csharp
// 修改前：尝试TCP连接
using var client = new TcpClient();
await client.ConnectAsync(host, port);

// 修改后：使用Redis连接测试
var tempConnection = ConnectionMultiplexer.Connect(_connectionString);
if (tempConnection.IsConnected)
{
    var database = tempConnection.GetDatabase();
    var pingResult = await database.PingAsync();
}
```

### 问题2: 健康检查失败

**症状**: 诊断结果显示"无法获取连接状态"

**原因**: 健康检查依赖于已建立的连接，但连接可能未正确初始化

**解决方案**:
1. 在健康检查中自动创建连接
2. 更新主连接实例

**修复代码**:
```csharp
// 检查现有连接或创建新连接
var connection = _connectionMultiplexer;
if (connection?.IsConnected != true)
{
    connection = ConnectionMultiplexer.Connect(_connectionString);
}
```

### 问题3: 连接字符串配置错误

**症状**: 所有Redis操作都失败

**原因**: 连接字符串未正确配置或获取

**解决方案**:
1. 确保使用Aspire配置方式
2. 验证连接字符串获取逻辑

**正确配置**:
```csharp
// 在构造函数中
_connectionString = _configuration.GetConnectionString("cache") ?? "cache:6379";
```

## 诊断工具

### 1. PowerShell测试脚本
```powershell
# 运行Aspire环境专用测试脚本
.\test_redis_aspire.ps1
```

### 2. API诊断端点
- `POST /api/redisdiagnostic/quick-test` - 快速连接测试
- `POST /api/redisdiagnostic/diagnose` - 完整诊断
- `GET /api/redisdiagnostic/stats` - 连接统计
- `GET /api/redisdiagnostic/errors` - 错误详情

### 3. Aspire仪表板
- 访问 `https://localhost:5000` 查看Aspire仪表板
- 检查Redis服务状态
- 查看服务日志

## 排查步骤

### 步骤1: 检查Aspire应用状态
```bash
# 确保Aspire应用正在运行
dotnet run --project AspireApp.AppHost
```

### 步骤2: 验证服务注册
检查 `AspireApp.AppHost/Program.cs`:
```csharp
var cache = builder.AddRedis("cache");
var apiService = builder.AddProject<Projects.WebApplication_Drone>("apisercie-drone")
    .WithReference(cache)  // 确保引用了Redis服务
    .WaitFor(cache);       // 确保等待Redis服务启动
```

### 步骤3: 检查配置注入
检查 `WebApplication_Drone/Program.cs`:
```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("cache");
});
```

### 步骤4: 运行诊断测试
```powershell
# 运行专用测试脚本
.\test_redis_aspire.ps1
```

### 步骤5: 查看日志
```bash
# 查看应用日志
dotnet run --project AspireApp.AppHost --verbosity detailed
```

## 性能优化建议

### 1. 连接池配置
```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("cache");
    options.InstanceName = "AspireApp_";
});
```

### 2. 健康检查配置
```csharp
builder.Services.AddHealthChecks()
    .AddRedis(
        builder.Configuration.GetConnectionString("cache") ?? "cache:6379",
        name: "redis",
        tags: new[] { "ready", "cache" }
    );
```

### 3. 缓存策略
```csharp
// 内存缓存作为本地缓存
builder.Services.AddMemoryCache(options =>
{
    options.CompactionPercentage = 0.1;
    options.SizeLimit = 1000;
});
```

## 故障排除清单

### ✅ 基础检查
- [ ] Aspire应用是否正在运行
- [ ] Redis服务是否在Aspire仪表板中显示为健康
- [ ] 连接字符串是否正确获取

### ✅ 网络检查
- [ ] 服务引用是否正确配置
- [ ] 等待依赖是否正确设置
- [ ] 网络连接测试是否通过

### ✅ 权限检查
- [ ] Redis读写权限是否正常
- [ ] 连接池是否正常工作
- [ ] 性能测试是否通过

### ✅ 健康检查
- [ ] 健康检查端点是否响应
- [ ] Redis服务状态是否正常
- [ ] 连接统计是否正确

## 常见错误代码

| 错误代码 | 描述 | 解决方案 |
|---------|------|----------|
| `SocketException` | 网络连接失败 | 检查Aspire网络配置 |
| `RedisConnectionException` | Redis连接异常 | 验证连接字符串 |
| `TimeoutException` | 连接超时 | 检查服务依赖配置 |
| `ConfigurationException` | 配置错误 | 验证Aspire配置 |

## 最佳实践

### 1. 服务依赖管理
```csharp
// 确保正确的依赖顺序
var apiService = builder.AddProject<Projects.WebApplication_Drone>("apisercie-drone")
    .WithReference(cache)
    .WaitFor(cache);  // 等待Redis服务启动
```

### 2. 错误处理
```csharp
// 在服务中使用适当的错误处理
try
{
    var result = await _cacheService.GetAsync<string>(key);
    return result;
}
catch (RedisConnectionException ex)
{
    _logger.LogError(ex, "Redis连接失败");
    return null;
}
```

### 3. 监控和日志
```csharp
// 添加详细的日志记录
_logger.LogInformation("Redis诊断服务初始化，连接字符串: {ConnectionString}", _connectionString);
```

## 总结

Aspire环境中的Redis连接问题主要源于：
1. 服务发现机制的不同
2. 网络架构的特殊性
3. 配置注入方式的差异

通过使用专门的诊断工具和遵循最佳实践，可以有效解决这些问题并确保Redis服务在Aspire环境中正常工作。 