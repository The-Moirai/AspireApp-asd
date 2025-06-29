using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebApplication_Drone.Services.Interfaces;
using WebApplication_Drone.Services.Models;
using System.Diagnostics;

namespace WebApplication_Drone.Services.Base
{
    /// <summary>
    /// 基础数据服务 - 提供通用的缓存、日志、错误处理等功能
    /// </summary>
    public abstract class BaseDataService
    {
        protected readonly ILogger _logger;
        protected readonly IMemoryCache _memoryCache;
        protected readonly DataServiceOptions _options;
        protected readonly SemaphoreSlim _operationSemaphore;
        
        // 性能计数器
        protected long _totalOperations = 0;
        protected long _cacheHits = 0;
        protected long _cacheMisses = 0;
        protected long _totalResponseTime = 0;
        protected long _totalExceptions = 0;
        
        // 缓存键前缀
        protected const string CACHE_KEY_PREFIX = "data:";
        protected const string CACHE_KEY_ALL_DRONES = "drones:all";
        protected const string CACHE_KEY_ALL_TASKS = "tasks:all";
        
        protected BaseDataService(
            ILogger logger,
            IMemoryCache memoryCache,
            IOptions<DataServiceOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _operationSemaphore = new SemaphoreSlim(_options.MaxConcurrentOperations, _options.MaxConcurrentOperations);
        }
        
        /// <summary>
        /// 执行带缓存的异步操作
        /// </summary>
        protected async Task<T> ExecuteWithCacheAsync<T>(
            string cacheKey,
            Func<Task<T>> dataProvider,
            TimeSpan? expiration = null)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                // 尝试从缓存获取
                if (_memoryCache.TryGetValue(cacheKey, out T? cachedValue))
                {
                    Interlocked.Increment(ref _cacheHits);
                    _logger.LogDebug("缓存命中: {CacheKey}", cacheKey);
                    return cachedValue!;
                }
                
                Interlocked.Increment(ref _cacheMisses);
                
                // 从数据源获取
                var value = await ExecuteWithRetryAsync(dataProvider);
                
                // 更新缓存
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(_options.CacheExpirationMinutes),
                    SlidingExpiration = TimeSpan.FromMinutes(_options.CacheExpirationMinutes / 2),
                    Priority = CacheItemPriority.Normal
                };
                _memoryCache.Set(cacheKey, value, cacheOptions);
                
                return value;
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _totalExceptions);
                _logger.LogError(ex, "执行缓存操作失败: {CacheKey}, 错误: {Message}", cacheKey, ex.Message);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        /// <summary>
        /// 执行带重试的异步操作
        /// </summary>
        protected async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation)
        {
            var attempts = 0;
            var lastException = default(Exception);
            
            while (attempts < _options.MaxRetryAttempts)
            {
                try
                {
                    await _operationSemaphore.WaitAsync();
                    try
                    {
                        return await operation();
                    }
                    finally
                    {
                        _operationSemaphore.Release();
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    attempts++;
                    
                    if (attempts < _options.MaxRetryAttempts)
                    {
                        var delay = TimeSpan.FromMilliseconds(Math.Pow(2, attempts) * 100); // 指数退避
                        _logger.LogWarning(ex, "操作失败，第 {Attempt} 次重试，延迟: {Delay}ms", attempts, delay.TotalMilliseconds);
                        await Task.Delay(delay);
                    }
                }
            }
            
            _logger.LogError(lastException, "操作在 {MaxAttempts} 次尝试后失败", _options.MaxRetryAttempts);
            throw lastException!;
        }
        
        /// <summary>
        /// 执行不带缓存的异步操作
        /// </summary>
        protected async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                return await ExecuteWithRetryAsync(operation);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _totalExceptions);
                _logger.LogError(ex, "执行操作失败: {Message}", ex.Message);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        /// <summary>
        /// 执行无返回值的异步操作
        /// </summary>
        protected async Task ExecuteAsync(Func<Task> operation)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalOperations);
            
            try
            {
                await ExecuteWithRetryAsync(async () =>
                {
                    await operation();
                    return true;
                });
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _totalExceptions);
                _logger.LogError(ex, "执行操作失败: {Message}", ex.Message);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Interlocked.Add(ref _totalResponseTime, stopwatch.ElapsedMilliseconds);
            }
        }
        
        /// <summary>
        /// 清除缓存
        /// </summary>
        protected void ClearCache(string pattern)
        {
            try
            {
                if (_memoryCache is MemoryCache memoryCache)
                {
                    // 注意：MemoryCache 没有内置的模式清除功能
                    // 这里只是记录日志，实际清除需要在具体实现中处理
                    _logger.LogDebug("清除缓存模式: {Pattern}", pattern);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清除缓存失败: {Pattern}", pattern);
            }
        }
        
        /// <summary>
        /// 获取性能统计信息
        /// </summary>
        protected DataServiceStatistics GetBaseStatistics()
        {
            return new DataServiceStatistics
            {
                TotalOperations = _totalOperations,
                CacheHits = _cacheHits,
                CacheMisses = _cacheMisses,
                AverageResponseTimeMs = _totalOperations > 0 ? (double)_totalResponseTime / _totalOperations : 0,
                LastUpdateTime = DateTime.UtcNow
            };
        }
        
        /// <summary>
        /// 记录操作日志
        /// </summary>
        protected void LogOperation(string operation, string? details = null)
        {
            if (_options.EnablePerformanceMonitoring)
            {
                _logger.LogDebug("执行操作: {Operation} {Details}", operation, details ?? "");
            }
        }
    }
} 