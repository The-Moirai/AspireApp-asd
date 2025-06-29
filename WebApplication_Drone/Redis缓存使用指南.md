# Redis缓存使用指南

## 概述

本系统已在Aspire环境下集成Redis分布式缓存，提供高性能的缓存解决方案。Redis缓存服务支持内存缓存和分布式缓存的混合使用，确保最佳的性能和可用性。

## 架构设计

### 1. 双层缓存架构
- **Redis分布式缓存**: 持久化存储，支持多实例共享
- **内存缓存**: 本地高速缓存，减少网络延迟
- **智能缓存策略**: 优先从内存获取，Redis作为后备

### 2. 缓存服务 (RedisCacheService)
- **位置**: `WebApplication_Drone/Services/RedisCacheService.cs`
- **功能**: 统一的缓存操作接口
- **特性**: 自动序列化、错误处理、性能统计

## 配置说明

### 1. Aspire环境配置

#### AspireApp.AppHost/Program.cs
```csharp
var cache = builder.AddRedis("cache");

var apiService = builder.AddProject<Projects.WebApplication_Drone>("apisercie-drone")
    .WithExternalHttpEndpoints()
    .WithReference(cache)  // 引用Redis服务
    .WaitFor(cache)        // 等待Redis启动
    .WithReference(db)
    .WaitFor(db);
```

#### WebApplication_Drone/Program.cs
```csharp
// Redis分布式缓存配置
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("cache") ?? "cache:6379";
    options.InstanceName = "AspireApp_";
});

// 内存缓存配置
builder.Services.AddMemoryCache(options =>
{
    options.CompactionPercentage = 0.1;
    options.SizeLimit = 1000;
});

// 注册Redis缓存服务
builder.Services.AddSingleton<RedisCacheService>();
```

### 2. 配置文件

#### appsettings.json
```json
{
  "ConnectionStrings": {
    "cache": "cache:6379"
  },
  "Cache": {
    "DefaultExpirationMinutes": 30,
    "MemoryCacheSize": 1000,
    "EnableDistributedCache": true
  }
}
```

## API使用

### 1. 缓存控制器 (/api/cache)

#### 基本操作
```http
# 获取缓存项
GET /api/cache/{key}

# 设置缓存项
POST /api/cache/{key}?expirationMinutes=30
Content-Type: application/json
{
  "message": "测试数据",
  "timestamp": "2024-01-01T00:00:00Z"
}

# 删除缓存项
DELETE /api/cache/{key}

# 刷新缓存项
PUT /api/cache/{key}/refresh

# 检查缓存项是否存在
GET /api/cache/{key}/exists
```

#### 批量操作
```http
# 批量获取
POST /api/cache/batch/get
Content-Type: application/json
["key1", "key2", "key3"]

# 批量设置
POST /api/cache/batch/set?expirationMinutes=30
Content-Type: application/json
{
  "key1": {"value": "data1"},
  "key2": {"value": "data2"},
  "key3": {"value": "data3"}
}
```

#### 管理操作
```http
# 获取缓存统计
GET /api/cache/statistics

# 获取健康状态
GET /api/cache/health

# 清空所有缓存
POST /api/cache/clear

# 性能测试
POST /api/cache/test/performance?iterations=1000
```

### 2. 服务层使用

#### 在DroneService中使用
```csharp
public class DroneService
{
    private readonly RedisCacheService _cacheService;
    
    public async Task<List<Drone>> GetDronesAsync()
    {
        // 尝试从缓存获取
        var cachedDrones = await _cacheService.GetAsync<List<Drone>>("drones:all");
        if (cachedDrones != null)
        {
            return cachedDrones;
        }
        
        // 从数据库获取
        var drones = await _droneRepository.GetAllAsync();
        
        // 设置缓存
        await _cacheService.SetAsync("drones:all", drones, TimeSpan.FromMinutes(30));
        
        return drones;
    }
}
```

#### 在TaskService中使用
```csharp
public class TaskService
{
    private readonly RedisCacheService _cacheService;
    
    public async Task<MainTask?> GetTaskAsync(Guid id)
    {
        var cacheKey = $"task:{id}";
        var cachedTask = await _cacheService.GetAsync<MainTask>(cacheKey);
        if (cachedTask != null)
        {
            return cachedTask;
        }
        
        var task = await _taskRepository.GetByIdAsync(id);
        if (task != null)
        {
            await _cacheService.SetAsync(cacheKey, task, TimeSpan.FromMinutes(30));
        }
        
        return task;
    }
}
```

## 缓存策略

### 1. 缓存键命名规范
```
{服务名}:{数据类型}:{标识符}
例如：
- drones:all          // 所有无人机
- drone:{id}          // 单个无人机
- tasks:all           // 所有任务
- task:{id}           // 单个任务
- performance:metrics // 性能指标
```

### 2. 过期时间策略
- **短期缓存**: 1-5分钟（高频访问数据）
- **中期缓存**: 10-30分钟（业务数据）
- **长期缓存**: 1-24小时（配置数据）

### 3. 缓存更新策略
- **写入时更新**: 数据变更时立即更新缓存
- **定时刷新**: 定期刷新关键数据
- **事件驱动**: 基于业务事件更新缓存

## 性能优化

### 1. 内存缓存优化
```csharp
// 配置内存缓存大小限制
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1000; // 限制缓存项数量
    options.CompactionPercentage = 0.1; // 压缩比例
});
```

### 2. Redis连接优化
```csharp
// 配置Redis连接池
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "cache:6379,connectTimeout=5000,syncTimeout=5000";
    options.InstanceName = "AspireApp_";
});
```

### 3. 序列化优化
```csharp
// 使用高效的JSON序列化
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = false
};
```

## 监控和诊断

### 1. 缓存统计
```http
GET /api/cache/statistics
```
返回数据：
```json
{
  "success": true,
  "data": {
    "timestamp": "2024-01-01T00:00:00Z",
    "memoryCacheSize": 150,
    "redisConnected": true,
    "totalKeys": 1000,
    "hitRate": 85.5,
    "missRate": 14.5
  }
}
```

### 2. 健康检查
```http
GET /api/cache/health
```
返回数据：
```json
{
  "success": true,
  "data": {
    "status": "Healthy",
    "redisConnected": true,
    "timestamp": "2024-01-01T00:00:00Z",
    "memoryCacheSize": 150,
    "totalKeys": 1000
  }
}
```

### 3. 性能测试
```http
POST /api/cache/test/performance?iterations=1000
```
返回数据：
```json
{
  "success": true,
  "data": {
    "iterations": 1000,
    "duration": 1250,
    "operationsPerSecond": 1600.0,
    "averageOperationTime": 0.625
  }
}
```

## 最佳实践

### 1. 缓存设计原则
- **缓存热点数据**: 优先缓存访问频率高的数据
- **避免缓存大对象**: 大对象会影响性能
- **设置合理的过期时间**: 平衡数据新鲜度和性能
- **使用批量操作**: 减少网络往返次数

### 2. 错误处理
```csharp
try
{
    var value = await _cacheService.GetAsync<T>(key);
    return value;
}
catch (Exception ex)
{
    _logger.LogError(ex, "缓存操作失败");
    // 降级到数据库查询
    return await _repository.GetAsync(id);
}
```

### 3. 缓存穿透防护
```csharp
public async Task<T?> GetWithNullProtection<T>(string key, Func<Task<T>> factory)
{
    var value = await _cacheService.GetAsync<T>(key);
    if (value != null)
    {
        return value;
    }
    
    // 防止缓存穿透
    var lockKey = $"lock:{key}";
    if (await _cacheService.ExistsAsync(lockKey))
    {
        // 等待其他线程完成
        await Task.Delay(100);
        return await _cacheService.GetAsync<T>(key);
    }
    
    // 设置锁
    await _cacheService.SetAsync(lockKey, true, TimeSpan.FromSeconds(10));
    
    try
    {
        value = await factory();
        if (value != null)
        {
            await _cacheService.SetAsync(key, value, TimeSpan.FromMinutes(30));
        }
        return value;
    }
    finally
    {
        await _cacheService.RemoveAsync(lockKey);
    }
}
```

## 故障排查

### 1. 常见问题

#### Redis连接失败
- 检查Redis服务是否启动
- 验证连接字符串配置
- 检查网络连接

#### 缓存性能下降
- 检查内存使用情况
- 分析缓存命中率
- 优化缓存策略

#### 数据不一致
- 检查缓存更新逻辑
- 验证过期时间设置
- 确认缓存清除策略

### 2. 调试方法
```csharp
// 启用详细日志
"Logging": {
  "LogLevel": {
    "WebApplication_Drone.Services.RedisCacheService": "Debug"
  }
}
```

### 3. 性能监控
- 监控缓存命中率
- 跟踪缓存操作延迟
- 分析内存使用趋势

## 扩展功能

### 1. 自定义缓存策略
```csharp
public class CustomCacheService : RedisCacheService
{
    public async Task<T> GetWithRetry<T>(string key, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await GetAsync<T>(key);
            }
            catch (Exception ex)
            {
                if (i == maxRetries - 1) throw;
                await Task.Delay(100 * (i + 1));
            }
        }
        return default(T);
    }
}
```

### 2. 缓存事件
```csharp
public class CacheEventService
{
    public event EventHandler<CacheEventArgs> CacheHit;
    public event EventHandler<CacheEventArgs> CacheMiss;
    
    public void OnCacheHit(string key) => CacheHit?.Invoke(this, new CacheEventArgs(key));
    public void OnCacheMiss(string key) => CacheMiss?.Invoke(this, new CacheEventArgs(key));
}
```

## 总结

通过Redis缓存集成，系统获得了：

1. **高性能**: 双层缓存架构提供最佳性能
2. **高可用**: Redis分布式缓存确保数据持久性
3. **易扩展**: 支持水平扩展和负载均衡
4. **易监控**: 完整的监控和诊断功能
5. **易维护**: 统一的缓存管理接口

建议根据实际业务需求调整缓存策略，定期监控缓存性能，并根据访问模式优化缓存配置。 