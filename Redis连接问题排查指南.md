# Redis连接问题排查指南

## 概述

本文档提供了详细的Redis连接问题排查步骤和解决方案，帮助您快速定位和解决Redis连接问题。

## 问题排查步骤

### 1. 基础检查

#### 1.1 检查Redis服务状态
```bash
# 检查Redis容器是否运行
docker ps | grep redis

# 检查Redis服务状态
docker exec -it <redis-container-id> redis-cli ping
```

#### 1.2 检查网络连接
```bash
# 测试到Redis的网络连接
telnet cache 6379

# 或者使用nc命令
nc -zv cache 6379
```

#### 1.3 检查端口监听
```bash
# 检查6379端口是否被监听
netstat -tlnp | grep 6379
```

### 2. 配置检查

#### 2.1 连接字符串配置
检查以下配置文件中的Redis连接字符串：

**appsettings.json**
```json
{
  "ConnectionStrings": {
    "cache": "cache:6379"
  }
}
```

**AspireApp.AppHost/Program.cs**
```csharp
var cache = builder.AddRedis("cache");
```

#### 2.2 服务注册检查
确保在`Program.cs`中正确注册了Redis服务：

```csharp
// Redis分布式缓存配置
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("cache");
    options.InstanceName = "AspireApp_";
});

// 注册Redis缓存服务
builder.Services.AddSingleton<RedisCacheService>();

// 注册Redis诊断服务
builder.Services.AddSingleton<RedisConnectionDiagnosticService>();
```

### 3. 使用诊断工具

#### 3.1 快速测试
```http
POST /api/redisdiagnostic/quick-test
```

#### 3.2 完整诊断
```http
POST /api/redisdiagnostic/diagnose
```

#### 3.3 获取统计信息
```http
GET /api/redisdiagnostic/stats
```

#### 3.4 压力测试
```http
POST /api/redisdiagnostic/stress-test?iterations=100
```

#### 3.5 获取错误详情
```http
GET /api/redisdiagnostic/errors
```

### 4. 常见问题及解决方案

#### 4.1 连接超时
**症状**: 连接Redis时出现超时错误
**可能原因**:
- Redis服务未启动
- 网络连接问题
- 防火墙阻止

**解决方案**:
```csharp
// 增加连接超时时间
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "cache:6379,connectTimeout=10000,syncTimeout=10000";
    options.InstanceName = "AspireApp_";
});
```

#### 4.2 连接被拒绝
**症状**: 出现"Connection refused"错误
**可能原因**:
- Redis服务未启动
- 端口配置错误
- 权限问题

**解决方案**:
1. 检查Redis服务状态
2. 验证端口配置
3. 检查防火墙设置

#### 4.3 认证失败
**症状**: 出现认证错误
**可能原因**:
- Redis配置了密码认证
- 用户名/密码错误

**解决方案**:
```csharp
// 如果Redis配置了密码
options.Configuration = "cache:6379,password=your_password";
```

#### 4.4 内存不足
**症状**: Redis操作失败，出现内存相关错误
**可能原因**:
- Redis内存使用过高
- 内存配置不足

**解决方案**:
1. 检查Redis内存使用情况
2. 清理不必要的缓存数据
3. 增加Redis内存限制

### 5. 日志分析

#### 5.1 启用详细日志
在`appsettings.json`中启用Redis相关日志：

```json
{
  "Logging": {
    "LogLevel": {
      "WebApplication_Drone.Services.RedisCacheService": "Debug",
      "WebApplication_Drone.Services.RedisConnectionDiagnosticService": "Debug",
      "Microsoft.Extensions.Caching.StackExchangeRedis": "Debug"
    }
  }
}
```

#### 5.2 常见日志信息
- `从内存缓存获取`: 表示从本地内存缓存获取数据
- `从Redis缓存获取`: 表示从Redis获取数据
- `Redis设置缓存失败，降级到内存缓存`: 表示Redis连接失败，降级到内存缓存
- `获取缓存失败`: 表示缓存操作完全失败

### 6. 性能优化

#### 6.1 连接池配置
```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "cache:6379,connectTimeout=5000,syncTimeout=5000,responseTimeout=5000";
    options.InstanceName = "AspireApp_";
});
```

#### 6.2 内存缓存优化
```csharp
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1000; // 限制缓存项数量
    options.CompactionPercentage = 0.1; // 压缩比例
});
```

### 7. 监控和告警

#### 7.1 健康检查
```http
GET /health
```

#### 7.2 缓存统计
```http
GET /api/cache/statistics
```

#### 7.3 性能监控
```http
GET /api/performance/overview
```

### 8. 故障恢复

#### 8.1 自动重连
Redis客户端通常支持自动重连，但可以手动触发：

```http
POST /api/redisdiagnostic/reset
```

#### 8.2 降级策略
当Redis不可用时，系统会自动降级到内存缓存：

```csharp
// 在RedisCacheService中已实现降级逻辑
catch (Exception ex)
{
    // Redis设置失败时，只设置内存缓存作为降级方案
    _logger.LogWarning(ex, "Redis设置缓存失败，降级到内存缓存: {Key}", key);
    
    try
    {
        var memoryExpiration = TimeSpan.FromMinutes(Math.Min(5, (expiration ?? TimeSpan.FromMinutes(30)).TotalMinutes));
        _memoryCache.Set(key, value, memoryExpiration);
        _logger.LogDebug("降级设置内存缓存成功: {Key}", key);
    }
    catch (Exception memoryEx)
    {
        _logger.LogError(memoryEx, "内存缓存设置也失败: {Key}", key);
    }
}
```

### 9. 预防措施

#### 9.1 定期监控
- 监控Redis连接状态
- 监控内存使用情况
- 监控响应时间

#### 9.2 容量规划
- 根据业务需求规划Redis容量
- 设置合理的过期时间
- 定期清理过期数据

#### 9.3 备份策略
- 定期备份Redis数据
- 配置Redis持久化
- 测试恢复流程

## 总结

通过以上步骤，您可以系统地排查和解决Redis连接问题。建议按照以下顺序进行：

1. **基础检查**: 确认Redis服务状态和网络连接
2. **配置检查**: 验证连接字符串和服务注册
3. **诊断工具**: 使用提供的API进行详细诊断
4. **日志分析**: 查看详细日志信息
5. **问题解决**: 根据具体问题采取相应措施
6. **预防措施**: 建立监控和备份机制

如果问题仍然存在，请收集以下信息：
- 完整的错误日志
- 诊断API的返回结果
- 系统环境信息
- 网络配置信息

然后联系技术支持团队进行进一步协助。 