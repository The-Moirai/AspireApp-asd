using Microsoft.AspNetCore.Mvc;
using WebApplication_Drone.Services.Clean;
using WebApplication_Drone.Services;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using ClassLibrary_Core.Mission;

namespace WebApplication_Drone.Controllers
{
    /// <summary>
    /// 测试控制器 - 用于测试性能监控功能
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly DroneService _droneService;
        private readonly TaskService _taskService;
        private readonly PerformanceMonitoringService _performanceService;
        private readonly ILogger<TestController> _logger;

        public TestController(
            DroneService droneService,
            TaskService taskService,
            PerformanceMonitoringService performanceService,
            ILogger<TestController> logger)
        {
            _droneService = droneService;
            _taskService = taskService;
            _performanceService = performanceService;
            _logger = logger;
        }

        /// <summary>
        /// 测试性能监控 - 模拟高负载
        /// </summary>
        [HttpPost("performance/load")]
        public async Task<IActionResult> TestPerformanceLoad([FromQuery] int duration = 10, [FromQuery] int intensity = 5)
        {
            try
            {
                _logger.LogInformation("开始性能负载测试 - 持续时间: {Duration}秒, 强度: {Intensity}", duration, intensity);
                
                var stopwatch = Stopwatch.StartNew();
                var tasks = new List<Task>();

                // 创建多个并发任务来模拟负载
                for (int i = 0; i < intensity; i++)
                {
                    tasks.Add(SimulateLoad(duration));
                }

                await Task.WhenAll(tasks);
                stopwatch.Stop();

                var result = new
                {
                    success = true,
                    message = "性能负载测试完成",
                    data = new
                    {
                        duration = duration,
                        intensity = intensity,
                        actualDuration = stopwatch.ElapsedMilliseconds,
                        tasksCompleted = tasks.Count
                    }
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "性能负载测试失败");
                return StatusCode(500, new { error = "性能负载测试失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 测试缓存性能
        /// </summary>
        [HttpPost("cache/performance")]
        public async Task<IActionResult> TestCachePerformance([FromQuery] int iterations = 100)
        {
            try
            {
                _logger.LogInformation("开始缓存性能测试 - 迭代次数: {Iterations}", iterations);
                
                var stopwatch = Stopwatch.StartNew();
                var droneStats = _droneService.GetStatistics();
                var taskStats = _taskService.GetStatistics();

                // 模拟缓存操作
                for (int i = 0; i < iterations; i++)
                {
                    await _droneService.GetAllDronesAsync();
                    await _taskService.GetAllTasksAsync();
                    
                    if (i % 10 == 0)
                    {
                        Thread.Sleep(10); // 短暂延迟
                    }
                }

                stopwatch.Stop();

                var finalDroneStats = _droneService.GetStatistics();
                var finalTaskStats = _taskService.GetStatistics();

                var result = new
                {
                    success = true,
                    message = "缓存性能测试完成",
                    data = new
                    {
                        iterations = iterations,
                        duration = stopwatch.ElapsedMilliseconds,
                        droneCacheHits = finalDroneStats.CacheHits - droneStats.CacheHits,
                        droneCacheMisses = finalDroneStats.CacheMisses - droneStats.CacheMisses,
                        taskCacheHits = finalTaskStats.CacheHits - taskStats.CacheHits,
                        taskCacheMisses = finalTaskStats.CacheMisses - taskStats.CacheMisses,
                        averageResponseTime = stopwatch.ElapsedMilliseconds / (double)iterations
                    }
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "缓存性能测试失败");
                return StatusCode(500, new { error = "缓存性能测试失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 测试内存使用
        /// </summary>
        [HttpPost("memory/test")]
        public IActionResult TestMemoryUsage([FromQuery] int sizeMB = 100)
        {
            try
            {
                _logger.LogInformation("开始内存使用测试 - 大小: {SizeMB}MB", sizeMB);
                
                var beforeMemory = GC.GetTotalMemory(false);
                var stopwatch = Stopwatch.StartNew();

                // 分配内存
                var memoryBlocks = new List<byte[]>();
                var blockSize = 1024 * 1024; // 1MB
                var blocks = sizeMB;

                for (int i = 0; i < blocks; i++)
                {
                    memoryBlocks.Add(new byte[blockSize]);
                    if (i % 10 == 0)
                    {
                        Thread.Sleep(1); // 短暂延迟
                    }
                }

                stopwatch.Stop();
                var afterMemory = GC.GetTotalMemory(false);
                var allocatedMemory = afterMemory - beforeMemory;

                // 清理内存
                memoryBlocks.Clear();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var finalMemory = GC.GetTotalMemory(false);

                var result = new
                {
                    success = true,
                    message = "内存使用测试完成",
                    data = new
                    {
                        requestedSizeMB = sizeMB,
                        allocatedMemoryMB = allocatedMemory / 1024 / 1024,
                        finalMemoryMB = finalMemory / 1024 / 1024,
                        duration = stopwatch.ElapsedMilliseconds,
                        memoryCleaned = (allocatedMemory - (finalMemory - beforeMemory)) / 1024 / 1024
                    }
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "内存使用测试失败");
                return StatusCode(500, new { error = "内存使用测试失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 测试数据库连接
        /// </summary>
        [HttpPost("database/test")]
        public async Task<IActionResult> TestDatabaseConnection()
        {
            try
            {
                _logger.LogInformation("开始数据库连接测试");
                
                var stopwatch = Stopwatch.StartNew();
                
                // 测试无人机服务数据库连接
                var droneStats = _droneService.GetStatistics();
                
                // 测试任务服务数据库连接
                var taskStats = _taskService.GetStatistics();
                
                stopwatch.Stop();

                var result = new
                {
                    success = true,
                    message = "数据库连接测试完成",
                    data = new
                    {
                        duration = stopwatch.ElapsedMilliseconds,
                        droneDatabaseConnected = droneStats.DatabaseConnected,
                        taskDatabaseConnected = taskStats.DatabaseConnected,
                        overallStatus = droneStats.DatabaseConnected && taskStats.DatabaseConnected ? "Connected" : "Disconnected"
                    }
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "数据库连接测试失败");
                return StatusCode(500, new { error = "数据库连接测试失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 获取测试结果汇总
        /// </summary>
        [HttpGet("summary")]
        public IActionResult GetTestSummary()
        {
            try
            {
                var droneStats = _droneService.GetStatistics();
                var taskStats = _taskService.GetStatistics();
                var performanceMetrics = _performanceService.GetCurrentMetrics();

                var summary = new
                {
                    timestamp = DateTime.UtcNow,
                    system = new
                    {
                        uptime = Process.GetCurrentProcess().StartTime,
                        memoryUsage = performanceMetrics?.MemoryUsageMB ?? 0,
                        cpuUsage = performanceMetrics?.CpuUsagePercent ?? 0,
                        threadCount = performanceMetrics?.ThreadCount ?? 0
                    },
                    services = new
                    {
                        drone = new
                        {
                            totalOperations = droneStats.TotalOperations,
                            cacheHitRate = droneStats.CacheHitRate,
                            averageResponseTime = droneStats.AverageResponseTimeMs,
                            databaseConnected = droneStats.DatabaseConnected
                        },
                        task = new
                        {
                            totalOperations = taskStats.TotalOperations,
                            cacheHitRate = taskStats.CacheHitRate,
                            averageResponseTime = taskStats.AverageResponseTimeMs,
                            databaseConnected = taskStats.DatabaseConnected
                        }
                    },
                    performance = new
                    {
                        requestsPerSecond = performanceMetrics?.RequestsPerSecond ?? 0,
                        totalRequests = performanceMetrics?.TotalRequests ?? 0,
                        totalExceptions = performanceMetrics?.TotalExceptions ?? 0,
                        averageResponseTime = performanceMetrics?.AverageResponseTimeMs ?? 0
                    }
                };

                return Ok(new { success = true, data = summary });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取测试汇总失败");
                return StatusCode(500, new { error = "获取测试汇总失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 模拟负载的私有方法
        /// </summary>
        private async Task SimulateLoad(int duration)
        {
            var stopwatch = Stopwatch.StartNew();
            
            while (stopwatch.ElapsedMilliseconds < duration * 1000)
            {
                // 模拟CPU密集型操作
                var result = 0;
                for (int i = 0; i < 1000; i++)
                {
                    result += i * i;
                }

                // 模拟内存分配
                var tempArray = new byte[1024];
                
                // 模拟I/O操作
                await Task.Delay(10);
            }
        }

        /// <summary>
        /// 测试Redis缓存功能
        /// </summary>
        [HttpPost("cache/test")]
        public async Task<IActionResult> TestCache()
        {
            try
            {
                _logger.LogInformation("开始测试Redis缓存功能");
                
                var cacheService = HttpContext.RequestServices.GetRequiredService<RedisCacheService>();
                
                // 测试设置缓存
                var testData = new { message = "测试数据", timestamp = DateTime.Now };
                await cacheService.SetAsync("test:cache", testData, TimeSpan.FromMinutes(5));
                _logger.LogInformation("缓存设置成功");
                
                // 测试获取缓存
                var retrievedData = await cacheService.GetAsync<object>("test:cache");
                if (retrievedData != null)
                {
                    _logger.LogInformation("缓存获取成功");
                }
                else
                {
                    _logger.LogWarning("缓存获取失败");
                }
                
                // 测试移除缓存
                await cacheService.RemoveAsync("test:cache");
                _logger.LogInformation("缓存移除成功");
                
                return Ok(new { 
                    success = true, 
                    message = "Redis缓存测试完成",
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis缓存测试失败");
                return StatusCode(500, new { 
                    success = false, 
                    error = "Redis缓存测试失败", 
                    message = ex.Message 
                });
            }
        }

        /// <summary>
        /// 简单测试主任务创建
        /// </summary>
        [HttpGet("test-main-task")]
        public async Task<IActionResult> TestMainTask()
        {
            try
            {
                _logger.LogInformation("开始测试主任务创建");
                
                // 创建一个测试主任务
                var mainTask = new MainTask
                {
                    Id = Guid.NewGuid(),
                    Description = "简单测试主任务",
                    Status = TaskStatus.WaitingForActivation,
                    CreationTime = DateTime.Now
                };
                
                _logger.LogInformation("创建主任务: Id={MainTaskId}, Description={Description}, SubTasksCount={SubTasksCount}", 
                    mainTask.Id, mainTask.Description, mainTask.SubTasks?.Count ?? 0);
                
                // 添加主任务
                var addResult = await _taskService.AddTaskAsync(mainTask, "TestController");
                _logger.LogInformation("主任务添加结果: {Result}", addResult);
                
                // 立即查找主任务
                var foundTask = _taskService.GetTask(mainTask.Id);
                
                return Ok(new
                {
                    mainTaskId = mainTask.Id,
                    description = mainTask.Description,
                    addResult = addResult,
                    foundInMemory = foundTask != null,
                    subTasksCount = foundTask?.SubTasks?.Count ?? 0,
                    subTasksInitialized = foundTask?.SubTasks != null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试主任务创建失败");
                return StatusCode(500, new { error = "测试失败", message = ex.Message });
            }
        }

        /// <summary>
        /// 测试子任务创建和查找
        /// </summary>
        [HttpGet("test-subtask")]
        public async Task<IActionResult> TestSubTask()
        {
            try
            {
                _logger.LogInformation("开始测试子任务创建和查找");
                
                // 创建一个测试主任务
                var mainTask = new MainTask
                {
                    Id = Guid.NewGuid(),
                    Description = "测试主任务",
                    Status = TaskStatus.WaitingForActivation,
                    CreationTime = DateTime.Now
                };
                
                _logger.LogInformation("创建主任务: Id={MainTaskId}, Description={Description}", 
                    mainTask.Id, mainTask.Description);
                
                // 等待主任务添加完成
                var addResult = await _taskService.AddTaskAsync(mainTask, "TestController");
                _logger.LogInformation("主任务添加结果: {Result}", addResult);
                
                // 验证主任务是否已添加
                var foundMainTask = _taskService.GetTask(mainTask.Id);
                if (foundMainTask == null)
                {
                    _logger.LogWarning("主任务未找到，尝试从数据库重新加载");
                    await _taskService.LoadTasksFromDatabaseAsync();
                    foundMainTask = _taskService.GetTask(mainTask.Id);
                }
                
                if (foundMainTask == null)
                {
                    return BadRequest(new
                    {
                        error = "主任务添加失败",
                        mainTaskId = mainTask.Id,
                        addResult = addResult
                    });
                }
                
                _logger.LogInformation("主任务验证成功: Id={MainTaskId}, SubTasksCount={SubTasksCount}", 
                    foundMainTask.Id, foundMainTask.SubTasks?.Count ?? 0);
                
                // 创建子任务名称
                var subTaskName = $"{mainTask.Id}_5_5";
                
                // 创建子任务
                var subTask = new SubTask
                {
                    Id = Guid.NewGuid(),
                    Description = subTaskName,
                    Status = TaskStatus.WaitingForActivation,
                    CreationTime = DateTime.Now,
                    ParentTask = mainTask.Id,
                    ReassignmentCount = 0
                };
                
                _logger.LogInformation("创建子任务: Id={SubTaskId}, Description={Description}, ParentTask={ParentTask}", 
                    subTask.Id, subTask.Description, subTask.ParentTask);
                
                // 使用addSubTasks方法添加子任务
                _taskService.addSubTasks(mainTask.Id, subTask);
                
                // 等待一小段时间确保子任务添加完成
                await Task.Delay(100);
                
                // 立即查找子任务
                var foundTask = _taskService.GetTask(mainTask.Id);
                var foundSubTask = foundTask?.SubTasks?.FirstOrDefault(st => st.Description == subTaskName);
                
                _logger.LogInformation("查找结果: MainTaskFound={MainTaskFound}, SubTaskFound={SubTaskFound}, TotalSubTasks={TotalSubTasks}", 
                    foundTask != null, foundSubTask != null, foundTask?.SubTasks?.Count ?? 0);
                
                // 准备子任务列表
                var subTasksList = new List<object>();
                if (foundTask?.SubTasks != null)
                {
                    subTasksList = foundTask.SubTasks.Select(st => new { id = st.Id, description = st.Description }).Cast<object>().ToList();
                }
                
                return Ok(new
                {
                    mainTaskId = mainTask.Id,
                    subTaskName = subTaskName,
                    subTaskFound = foundSubTask != null,
                    subTaskId = foundSubTask?.Id,
                    allSubTasks = subTasksList,
                    mainTaskAddResult = addResult,
                    mainTaskFound = foundTask != null,
                    totalSubTasks = foundTask?.SubTasks?.Count ?? 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试子任务创建失败");
                return StatusCode(500, new { error = "测试失败", message = ex.Message });
            }
        }
    }
} 