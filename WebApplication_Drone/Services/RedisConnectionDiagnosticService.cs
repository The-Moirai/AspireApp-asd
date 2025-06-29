using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Diagnostics;
using System.Net.Sockets;

namespace WebApplication_Drone.Services
{
    /// <summary>
    /// Redis连接诊断服务
    /// 用于诊断Redis连接问题，支持Aspire环境
    /// </summary>
    public class RedisConnectionDiagnosticService
    {
        private readonly ILogger<RedisConnectionDiagnosticService> _logger;
        private readonly IConfiguration _configuration;
        private ConnectionMultiplexer? _connectionMultiplexer;
        private readonly string _connectionString;

        public RedisConnectionDiagnosticService(
            ILogger<RedisConnectionDiagnosticService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            
            // 从Aspire配置中获取Redis连接字符串
            _connectionString = _configuration.GetConnectionString("cache") ?? "cache:6379";
            _logger.LogInformation("Redis诊断服务初始化，连接字符串: {ConnectionString}", _connectionString);
        }

        /// <summary>
        /// 执行完整诊断
        /// </summary>
        public async Task<RedisDiagnosticResult> DiagnoseAsync()
        {
            var result = new RedisDiagnosticResult
            {
                Timestamp = DateTime.UtcNow,
                Tests = new List<RedisDiagnosticTest>()
            };

            _logger.LogInformation("开始Redis连接诊断");

            // 1. 基础连接测试
            result.Tests.Add(await TestBasicConnectionAsync());

            // 2. 配置检查
            result.Tests.Add(await TestConfigurationAsync());

            // 3. 网络连接测试
            result.Tests.Add(await TestNetworkConnectionAsync());

            // 4. 权限测试
            result.Tests.Add(await TestPermissionsAsync());

            // 5. 性能测试
            result.Tests.Add(await TestPerformanceAsync());

            // 6. 健康检查
            result.Tests.Add(await TestHealthAsync());

            // 计算总体健康状态
            var failedTests = result.Tests.Count(t => t.Status == RedisTestStatus.Failed);
            result.IsHealthy = failedTests == 0;
            result.Summary = failedTests > 0 ? $"有 {failedTests} 个测试失败" : "所有测试通过";

            _logger.LogInformation("Redis诊断完成，结果: {Summary}", result.Summary);

            return result;
        }

        /// <summary>
        /// 基础连接测试
        /// </summary>
        private async Task<RedisDiagnosticTest> TestBasicConnectionAsync()
        {
            var test = new RedisDiagnosticTest
            {
                Name = "基础连接测试",
                Description = "测试Redis基础连接功能"
            };

            var stopwatch = Stopwatch.StartNew();

            try
            {
                _connectionMultiplexer = ConnectionMultiplexer.Connect(_connectionString);
                var database = _connectionMultiplexer.GetDatabase();
                
                // 测试PING命令
                var pingResult = await database.PingAsync();
                
                stopwatch.Stop();
                test.Status = RedisTestStatus.Passed;
                test.Details = $"连接成功，响应时间: {pingResult.TotalMilliseconds}ms";
                test.Duration = stopwatch.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                test.Status = RedisTestStatus.Failed;
                test.Details = $"连接失败: {ex.Message}";
                test.Duration = stopwatch.ElapsedMilliseconds;
                test.ErrorMessage = ex.Message;
                test.ErrorType = ex.GetType().Name;
            }

            return test;
        }

        /// <summary>
        /// 配置检查
        /// </summary>
        private async Task<RedisDiagnosticTest> TestConfigurationAsync()
        {
            var test = new RedisDiagnosticTest
            {
                Name = "配置检查",
                Description = "检查Redis配置参数"
            };

            try
            {
                var configStatus = _connectionMultiplexer?.IsConnected == true ? "已连接" : "未初始化";
                test.Status = RedisTestStatus.Passed;
                test.Details = $"连接字符串: {_connectionString}; ConnectionMultiplexer: {configStatus}";
            }
            catch (Exception ex)
            {
                test.Status = RedisTestStatus.Failed;
                test.Details = $"配置检查失败: {ex.Message}";
                test.ErrorMessage = ex.Message;
                test.ErrorType = ex.GetType().Name;
            }

            return test;
        }

        /// <summary>
        /// 网络连接测试
        /// </summary>
        private async Task<RedisDiagnosticTest> TestNetworkConnectionAsync()
        {
            var test = new RedisDiagnosticTest
            {
                Name = "网络连接测试",
                Description = "测试网络连通性"
            };

            try
            {
                // 在Aspire环境中，服务名只在内部网络可解析
                // 直接通过Redis连接测试网络连通性，而不是TCP连接
                if (_connectionMultiplexer?.IsConnected == true)
                {
                    var database = _connectionMultiplexer.GetDatabase();
                    var pingResult = await database.PingAsync();
                    
                    test.Status = RedisTestStatus.Passed;
                    test.Details = $"网络连接正常，Redis响应时间: {pingResult.TotalMilliseconds}ms";
                    test.Duration = (long)pingResult.TotalMilliseconds;
                }
                else
                {
                    // 尝试建立连接来测试网络
                    var tempConnection = ConnectionMultiplexer.Connect(_connectionString);
                    if (tempConnection.IsConnected)
                    {
                        var database = tempConnection.GetDatabase();
                        var pingResult = await database.PingAsync();
                        tempConnection.Close();
                        
                        test.Status = RedisTestStatus.Passed;
                        test.Details = $"网络连接正常，Redis响应时间: {pingResult.TotalMilliseconds}ms";
                        test.Duration = (long)pingResult.TotalMilliseconds;
                    }
                    else
                    {
                        tempConnection.Close();
                        test.Status = RedisTestStatus.Failed;
                        test.Details = "无法建立Redis连接";
                        test.ErrorMessage = "无法建立Redis连接";
                        test.ErrorType = "ConnectionException";
                    }
                }
            }
            catch (Exception ex)
            {
                test.Status = RedisTestStatus.Failed;
                test.Details = $"网络连接测试失败: {ex.Message}";
                test.ErrorMessage = ex.Message;
                test.ErrorType = ex.GetType().Name;
            }

            return test;
        }

        /// <summary>
        /// 权限测试
        /// </summary>
        private async Task<RedisDiagnosticTest> TestPermissionsAsync()
        {
            var test = new RedisDiagnosticTest
            {
                Name = "权限测试",
                Description = "测试Redis读写权限"
            };

            try
            {
                if (_connectionMultiplexer?.IsConnected == true)
                {
                    var database = _connectionMultiplexer.GetDatabase();
                    var testKey = $"permission_test_{Guid.NewGuid()}";
                    var testValue = "test_value";

                    // 测试写入
                    await database.StringSetAsync(testKey, testValue, TimeSpan.FromMinutes(1));
                    
                    // 测试读取
                    var retrievedValue = await database.StringGetAsync(testKey);
                    
                    // 测试删除
                    await database.KeyDeleteAsync(testKey);

                    test.Status = RedisTestStatus.Passed;
                    test.Details = "读写删除权限正常";
                }
                else
                {
                    test.Status = RedisTestStatus.Failed;
                    test.Details = "Redis连接未建立，无法测试权限";
                    test.ErrorMessage = "Redis连接未建立";
                    test.ErrorType = "ConnectionException";
                }
            }
            catch (Exception ex)
            {
                test.Status = RedisTestStatus.Failed;
                test.Details = $"权限测试失败: {ex.Message}";
                test.ErrorMessage = ex.Message;
                test.ErrorType = ex.GetType().Name;
            }

            return test;
        }

        /// <summary>
        /// 性能测试
        /// </summary>
        private async Task<RedisDiagnosticTest> TestPerformanceAsync()
        {
            var test = new RedisDiagnosticTest
            {
                Name = "性能测试",
                Description = "测试Redis操作性能"
            };

            var stopwatch = Stopwatch.StartNew();
            var operations = 10;
            var successCount = 0;

            try
            {
                if (_connectionMultiplexer?.IsConnected == true)
                {
                    var database = _connectionMultiplexer.GetDatabase();

                    for (int i = 0; i < operations; i++)
                    {
                        try
                        {
                            var key = $"perf_test_{i}_{Guid.NewGuid()}";
                            var value = $"value_{i}";

                            await database.StringSetAsync(key, value, TimeSpan.FromMinutes(1));
                            var retrieved = await database.StringGetAsync(key);
                            await database.KeyDeleteAsync(key);

                            if (retrieved == value)
                            {
                                successCount++;
                            }
                        }
                        catch
                        {
                            // 忽略单个操作的错误
                        }
                    }

                    stopwatch.Stop();
                    var avgTime = operations > 0 ? stopwatch.ElapsedMilliseconds / operations : 0;

                    test.Status = successCount == operations ? RedisTestStatus.Passed : RedisTestStatus.Failed;
                    test.Details = $"性能测试完成，总时间: {stopwatch.ElapsedMilliseconds}ms, 平均: {avgTime}ms/次, 操作数: {operations}";
                    test.Duration = stopwatch.ElapsedMilliseconds;
                }
                else
                {
                    test.Status = RedisTestStatus.Failed;
                    test.Details = "Redis连接未建立，无法进行性能测试";
                    test.ErrorMessage = "Redis连接未建立";
                    test.ErrorType = "ConnectionException";
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                test.Status = RedisTestStatus.Failed;
                test.Details = $"性能测试失败: {ex.Message}";
                test.Duration = stopwatch.ElapsedMilliseconds;
                test.ErrorMessage = ex.Message;
                test.ErrorType = ex.GetType().Name;
            }

            return test;
        }

        /// <summary>
        /// 健康检查
        /// </summary>
        private async Task<RedisDiagnosticTest> TestHealthAsync()
        {
            var test = new RedisDiagnosticTest
            {
                Name = "健康检查",
                Description = "Redis服务健康状态检查"
            };

            try
            {
                // 检查现有连接或创建新连接
                var connection = _connectionMultiplexer;
                if (connection?.IsConnected != true)
                {
                    // 如果现有连接不可用，尝试创建新连接
                    connection = ConnectionMultiplexer.Connect(_connectionString);
                }

                if (connection.IsConnected)
                {
                    var database = connection.GetDatabase();
                    var pingResult = await database.PingAsync();
                    
                    test.Status = RedisTestStatus.Passed;
                    test.Details = $"Redis服务健康，PING响应时间: {pingResult.TotalMilliseconds}ms";
                    test.Duration = (long)pingResult.TotalMilliseconds;
                    
                    // 如果这是新创建的连接，更新主连接
                    if (_connectionMultiplexer?.IsConnected != true)
                    {
                        _connectionMultiplexer?.Close();
                        _connectionMultiplexer = connection;
                    }
                }
                else
                {
                    test.Status = RedisTestStatus.Failed;
                    test.Details = "Redis连接不可用";
                    test.ErrorMessage = "Redis连接不可用";
                    test.ErrorType = "ConnectionException";
                }
            }
            catch (Exception ex)
            {
                test.Status = RedisTestStatus.Failed;
                test.Details = $"健康检查失败: {ex.Message}";
                test.ErrorMessage = ex.Message;
                test.ErrorType = ex.GetType().Name;
            }

            return test;
        }

        /// <summary>
        /// 获取连接统计信息
        /// </summary>
        public async Task<RedisConnectionStats> GetConnectionStatsAsync()
        {
            var stats = new RedisConnectionStats();

            try
            {
                if (_connectionMultiplexer?.IsConnected == true)
                {
                    var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());
                    
                    stats.IsConnected = _connectionMultiplexer.IsConnected;
                    
                    // 获取服务器信息
                    var info = await server.InfoAsync();
                    // 通过INFO命令获取连接数和操作数
                    var clients = info.FirstOrDefault(g => g.Key == "Clients")?.FirstOrDefault(kv => kv.Key == "connected_clients").Value;
                    var ops = info.FirstOrDefault(g => g.Key == "Stats")?.FirstOrDefault(kv => kv.Key == "total_commands_processed").Value;
                    stats.ConnectionCount = clients != null ? long.Parse(clients) : 0;
                    stats.OperationCount = ops != null ? long.Parse(ops) : 0;
                    stats.ServerInfo = info.ToDictionary(group => group.Key, group => string.Join(", ", group.Select(kvp => kvp.Value)));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取Redis连接统计信息失败");
                stats.ErrorMessage = ex.Message;
            }

            return stats;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _connectionMultiplexer?.Close();
            _connectionMultiplexer?.Dispose();
        }
    }

    /// <summary>
    /// Redis诊断结果
    /// </summary>
    public class RedisDiagnosticResult
    {
        public DateTime Timestamp { get; set; }
        public bool IsHealthy { get; set; }
        public List<RedisDiagnosticTest> Tests { get; set; } = new();
        public string Summary { get; set; } = string.Empty;
    }

    /// <summary>
    /// Redis诊断测试
    /// </summary>
    public class RedisDiagnosticTest
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public RedisTestStatus Status { get; set; }
        public string? Details { get; set; }
        public long Duration { get; set; }
        public string? ErrorMessage { get; set; }  // 只存储错误消息，不存储Exception对象
        public string? ErrorType { get; set; }     // 存储异常类型名称
    }

    /// <summary>
    /// Redis测试状态
    /// </summary>
    public enum RedisTestStatus
    {
        Passed = 0,
        Failed = 1
    }

    /// <summary>
    /// Redis连接统计
    /// </summary>
    public class RedisConnectionStats
    {
        public bool IsConnected { get; set; }
        public long ConnectionCount { get; set; }
        public long OperationCount { get; set; }
        public Dictionary<string, string> ServerInfo { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }
} 