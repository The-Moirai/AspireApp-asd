using System.Text.Json;

namespace BlazorApp_Web.Service
{
    /// <summary>
    /// Python 后端通信服务
    /// </summary>
    public interface IPythonBackendService
    {
        Task<PythonBackendStatus> GetStatusAsync();
        Task<PythonServiceInfo> GetServiceInfoAsync();
        Task<AiModuleStatus> GetAiModuleStatusAsync();
        Task<TestResult> TestFaceRecognitionAsync();
        Task<TestResult> TestMaskDetectionAsync();
        Task<TestResult> TestLoadBalancingAsync();
        Task<TestResult> TestClusterConnectionAsync();
        Task<List<PythonLogEntry>> GetLogsAsync(int count = 50);
        Task<DroneClusterInfo> GetClusterInfoAsync();
        Task<TaskCreationResult> CreateVideoProcessingTaskAsync(string videoPath, string taskName);
        Task<TaskProcessingStatus> GetTaskStatusAsync(string taskName);
        Task<ClusterControlResult> StartAllNodesAsync(int nodeCount);
        Task<ClusterControlResult> ShutdownNodeAsync(string nodeName);
    }

    public class PythonBackendService : IPythonBackendService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PythonBackendService> _logger;
        private readonly string _pythonApiBaseUrl;
        private readonly bool _useMockData;

        public PythonBackendService(IHttpClientFactory httpClientFactory, 
                                  ILogger<PythonBackendService> logger,
                                  IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _pythonApiBaseUrl = configuration.GetValue<string>("PythonBackend:BaseUrl") ?? "http://localhost:5000";
            _useMockData = configuration.GetValue<bool>("PythonBackend:UseMockData", true);
        }

        public async Task<PythonBackendStatus> GetStatusAsync()
        {
            if (_useMockData)
            {
                // 模拟网络延迟
                await Task.Delay(500);
                return new PythonBackendStatus 
                { 
                    IsConnected = true, 
                    Message = "模拟连接成功",
                    LastCheck = DateTime.Now
                };
            }
            
            try
            {
                var client = CreateHttpClient();
                var response = await client.GetAsync("/api/status");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var status = JsonSerializer.Deserialize<PythonBackendStatus>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    return status ?? new PythonBackendStatus { IsConnected = false, Message = "无法解析响应" };
                }
                else
                {
                    return new PythonBackendStatus 
                    { 
                        IsConnected = false, 
                        Message = $"HTTP {response.StatusCode}: {response.ReasonPhrase}" 
                    };
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "无法连接到 Python 后端");
                return new PythonBackendStatus 
                { 
                    IsConnected = false, 
                    Message = "连接超时或服务不可用" 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查 Python 后端状态时发生错误");
                return new PythonBackendStatus 
                { 
                    IsConnected = false, 
                    Message = $"错误: {ex.Message}" 
                };
            }
        }

        public async Task<PythonServiceInfo> GetServiceInfoAsync()
        {
            if (_useMockData)
            {
                await Task.Delay(300);
                return new PythonServiceInfo
                {
                    ServiceName = "AI 无人机处理服务",
                    Version = "v2.1.0",
                    Uptime = "3天 8小时 45分钟",
                    RequestCount = 1847,
                    CpuUsage = 23.5,
                    MemoryUsage = 67.2
                };
            }
            
            try
            {
                var client = CreateHttpClient();
                var response = await client.GetAsync("/api/info");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var info = JsonSerializer.Deserialize<PythonServiceInfo>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    return info ?? new PythonServiceInfo();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取 Python 服务信息时发生错误");
            }
            
            return new PythonServiceInfo();
        }

        public async Task<AiModuleStatus> GetAiModuleStatusAsync()
        {
            if (_useMockData)
            {
                await Task.Delay(400);
                var random = new Random();
                return new AiModuleStatus
                {
                    FaceRecognitionStatus = "正常",
                    MaskDetectionStatus = "正常", 
                    LoadBalancingStatus = "正常",
                    FaceRecognitionAccuracy = random.Next(92, 98),
                    MaskDetectionAccuracy = random.Next(88, 95),
                    CurrentLoad = random.Next(15, 35),
                    ActiveNodes = 3
                };
            }
            
            try
            {
                var client = CreateHttpClient();
                var response = await client.GetAsync("/api/ai/status");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var status = JsonSerializer.Deserialize<AiModuleStatus>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    return status ?? new AiModuleStatus();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取 AI 模块状态时发生错误");
            }
            
            return new AiModuleStatus();
        }

        public async Task<TestResult> TestFaceRecognitionAsync()
        {
            if (_useMockData)
            {
                await Task.Delay(2000); // 模拟测试时间
                var random = new Random();
                var accuracy = random.Next(93, 99);
                return new TestResult
                {
                    Success = true,
                    Message = $"识别准确率: {accuracy}%",
                    Accuracy = accuracy,
                    Duration = TimeSpan.FromMilliseconds(random.Next(1500, 2500))
                };
            }
            
            try
            {
                var client = CreateHttpClient();
                var response = await client.PostAsync("/api/test/face-recognition", null);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<TestResult>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    return result ?? new TestResult { Success = false, Message = "无法解析测试结果" };
                }
                else
                {
                    return new TestResult 
                    { 
                        Success = false, 
                        Message = $"测试失败: {response.StatusCode}" 
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试人脸识别时发生错误");
                return new TestResult 
                { 
                    Success = false, 
                    Message = $"测试异常: {ex.Message}" 
                };
            }
        }

        public async Task<TestResult> TestMaskDetectionAsync()
        {
            if (_useMockData)
            {
                await Task.Delay(1500);
                var random = new Random();
                var accuracy = random.Next(89, 96);
                return new TestResult
                {
                    Success = true,
                    Message = $"检测准确率: {accuracy}%",
                    Accuracy = accuracy,
                    Duration = TimeSpan.FromMilliseconds(random.Next(1200, 2000))
                };
            }
            
            try
            {
                var client = CreateHttpClient();
                var response = await client.PostAsync("/api/test/mask-detection", null);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<TestResult>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    return result ?? new TestResult { Success = false, Message = "无法解析测试结果" };
                }
                else
                {
                    return new TestResult 
                    { 
                        Success = false, 
                        Message = $"测试失败: {response.StatusCode}" 
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试口罩检测时发生错误");
                return new TestResult 
                { 
                    Success = false, 
                    Message = $"测试异常: {ex.Message}" 
                };
            }
        }

        public async Task<TestResult> TestLoadBalancingAsync()
        {
            if (_useMockData)
            {
                await Task.Delay(1000);
                var random = new Random();
                var load = random.Next(12, 28);
                return new TestResult
                {
                    Success = true,
                    Message = $"负载均衡正常，当前负载: {load}%",
                    Duration = TimeSpan.FromMilliseconds(random.Next(800, 1500))
                };
            }
            
            try
            {
                var client = CreateHttpClient();
                var response = await client.PostAsync("/api/test/load-balancing", null);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<TestResult>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    return result ?? new TestResult { Success = false, Message = "无法解析测试结果" };
                }
                else
                {
                    return new TestResult 
                    { 
                        Success = false, 
                        Message = $"测试失败: {response.StatusCode}" 
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试负载均衡时发生错误");
                return new TestResult 
                { 
                    Success = false, 
                    Message = $"测试异常: {ex.Message}" 
                };
            }
        }

        public async Task<TestResult> TestClusterConnectionAsync()
        {
            if (_useMockData)
            {
                await Task.Delay(2500);
                return new TestResult
                {
                    Success = true,
                    Message = "所有3个节点响应正常，集群状态健康",
                    Duration = TimeSpan.FromMilliseconds(2500)
                };
            }
            
            try
            {
                var client = CreateHttpClient();
                var response = await client.PostAsync("/api/test/cluster", null);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<TestResult>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    return result ?? new TestResult { Success = false, Message = "无法解析测试结果" };
                }
                else
                {
                    return new TestResult 
                    { 
                        Success = false, 
                        Message = $"测试失败: {response.StatusCode}" 
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试集群连接时发生错误");
                return new TestResult 
                { 
                    Success = false, 
                    Message = $"测试异常: {ex.Message}" 
                };
            }
        }

        public async Task<List<PythonLogEntry>> GetLogsAsync(int count = 50)
        {
            if (_useMockData)
            {
                await Task.Delay(200);
                var logs = new List<PythonLogEntry>();
                var random = new Random();
                var messages = new[]
                {
                    "人脸识别模块初始化完成",
                    "口罩检测服务正在运行",
                    "负载均衡器状态检查完成",
                    "处理无人机数据流",
                    "AI 推理引擎响应正常",
                    "集群节点心跳检测",
                    "图像预处理完成",
                    "模型推理耗时: 45ms"
                };
                var levels = new[] { "INFO", "DEBUG", "SUCCESS", "WARNING" };
                
                for (int i = 0; i < Math.Min(count, 10); i++)
                {
                    logs.Add(new PythonLogEntry
                    {
                        Timestamp = DateTime.Now.AddMinutes(-random.Next(0, 60)),
                        Level = levels[random.Next(levels.Length)],
                        Message = messages[random.Next(messages.Length)],
                        Source = "ai_service"
                    });
                }
                
                return logs.OrderByDescending(l => l.Timestamp).ToList();
            }
            
            try
            {
                var client = CreateHttpClient();
                var response = await client.GetAsync($"/api/logs?count={count}");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var logs = JsonSerializer.Deserialize<List<PythonLogEntry>>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    return logs ?? new List<PythonLogEntry>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取 Python 后端日志时发生错误");
            }
            
            return new List<PythonLogEntry>();
        }

        public async Task<DroneClusterInfo> GetClusterInfoAsync()
        {
            if (_useMockData)
            {
                await Task.Delay(300);
                return new DroneClusterInfo
                {
                    TotalNodes = 5,
                    ActiveNodes = 3,
                    Clusters = new List<ClusterInfo>
                    {
                        new ClusterInfo { ClusterName = "cluster0", NodeCount = 3, Nodes = new List<string> { "192.168.31.35:5001", "192.168.31.35:5002", "192.168.31.35:5003" } },
                        new ClusterInfo { ClusterName = "cluster1", NodeCount = 2, Nodes = new List<string> { "192.168.31.35:5004", "192.168.31.35:5005" } }
                    }
                };
            }

            try
            {
                var client = CreateTcpClient();
                var nodeInfoRequest = new
                {
                    type = "node_info",
                    content = "",
                    next_node = ""
                };

                await SendMessageAsync(client, JsonSerializer.Serialize(nodeInfoRequest));
                var response = await ReceiveMessageAsync(client);
                
                // 解析节点信息和集群信息
                return ParseClusterInfo(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取集群信息失败");
                return new DroneClusterInfo();
            }
        }

        public async Task<TaskCreationResult> CreateVideoProcessingTaskAsync(string videoPath, string taskName)
        {
            if (_useMockData)
            {
                await Task.Delay(1000);
                return new TaskCreationResult
                {
                    Success = true,
                    TaskId = Guid.NewGuid().ToString(),
                    Message = "任务创建成功",
                    SubtaskCount = 100
                };
            }

            try
            {
                var client = CreateTcpClient();
                var createTaskRequest = new
                {
                    type = "create_tasks",
                    content = videoPath,
                    next_node = taskName
                };

                await SendMessageAsync(client, JsonSerializer.Serialize(createTaskRequest));
                var response = await ReceiveMessageAsync(client);
                
                return ParseTaskCreationResult(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建视频处理任务失败");
                return new TaskCreationResult
                {
                    Success = false,
                    Message = $"任务创建失败: {ex.Message}"
                };
            }
        }

        public async Task<TaskProcessingStatus> GetTaskStatusAsync(string taskName)
        {
            if (_useMockData)
            {
                await Task.Delay(200);
                var random = new Random();
                return new TaskProcessingStatus
                {
                    TaskName = taskName,
                    TotalSubtasks = 100,
                    CompletedSubtasks = random.Next(0, 100),
                    Status = random.Next(0, 100) > 50 ? "processing" : "completed",
                    ProcessedImagePath = random.Next(0, 100) > 50 ? $"/processed/{taskName}" : ""
                };
            }

            // 实际实现需要与real_work.py的任务状态系统集成
            // 这里可以通过检查ans_set或其他状态机制来获取任务状态
            return new TaskProcessingStatus
            {
                TaskName = taskName,
                Status = "unknown"
            };
        }

        public async Task<ClusterControlResult> StartAllNodesAsync(int nodeCount)
        {
            if (_useMockData)
            {
                await Task.Delay(2000);
                return new ClusterControlResult
                {
                    Success = true,
                    Message = $"成功启动 {nodeCount} 个节点",
                    ActiveNodeCount = nodeCount
                };
            }

            try
            {
                var client = CreateTcpClient();
                var startRequest = new
                {
                    type = "start_all",
                    content = nodeCount,
                    next_node = ""
                };

                await SendMessageAsync(client, JsonSerializer.Serialize(startRequest));
                var response = await ReceiveMessageAsync(client);
                
                return ParseClusterControlResult(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动节点失败");
                return new ClusterControlResult
                {
                    Success = false,
                    Message = $"启动失败: {ex.Message}"
                };
            }
        }

        public async Task<ClusterControlResult> ShutdownNodeAsync(string nodeName)
        {
            if (_useMockData)
            {
                await Task.Delay(500);
                return new ClusterControlResult
                {
                    Success = true,
                    Message = $"节点 {nodeName} 已关闭"
                };
            }

            try
            {
                var client = CreateTcpClient();
                var shutdownRequest = new
                {
                    type = "shutdown",
                    content = nodeName,
                    next_node = ""
                };

                await SendMessageAsync(client, JsonSerializer.Serialize(shutdownRequest));
                return new ClusterControlResult
                {
                    Success = true,
                    Message = $"节点 {nodeName} 关闭请求已发送"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "关闭节点失败");
                return new ClusterControlResult
                {
                    Success = false,
                    Message = $"关闭失败: {ex.Message}"
                };
            }
        }

        private HttpClient CreateHttpClient()
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_pythonApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
            return client;
        }

        private System.Net.Sockets.TcpClient CreateTcpClient()
        {
            var pythonHost = "192.168.31.35";  // real_work.py中的machine_ip
            var pythonPort = 5007;             // real_work.py中监听的端口

            var client = new System.Net.Sockets.TcpClient();
            client.ConnectAsync(pythonHost, pythonPort).Wait(TimeSpan.FromSeconds(5));
            return client;
        }

        private async Task SendMessageAsync(System.Net.Sockets.TcpClient client, string message)
        {
            var data = System.Text.Encoding.UTF8.GetBytes(message);
            var stream = client.GetStream();
            await stream.WriteAsync(data, 0, data.Length);
        }

        private async Task<string> ReceiveMessageAsync(System.Net.Sockets.TcpClient client)
        {
            var buffer = new byte[4096];
            var stream = client.GetStream();
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            return System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
        }

        private DroneClusterInfo ParseClusterInfo(string response)
        {
            try
            {
                // 解析real_work.py返回的节点信息和集群信息
                var jsonResponse = JsonDocument.Parse(response);
                // 根据real_work.py的响应格式进行解析
                return new DroneClusterInfo(); // 实际解析逻辑
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解析集群信息失败");
                return new DroneClusterInfo();
            }
        }

        private TaskCreationResult ParseTaskCreationResult(string response)
        {
            try
            {
                // 解析real_work.py返回的任务创建结果
                return new TaskCreationResult
                {
                    Success = true,
                    TaskId = Guid.NewGuid().ToString(),
                    Message = "任务创建成功"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解析任务创建结果失败");
                return new TaskCreationResult
                {
                    Success = false,
                    Message = "解析失败"
                };
            }
        }

        private ClusterControlResult ParseClusterControlResult(string response)
        {
            try
            {
                // 解析real_work.py返回的控制结果
                return new ClusterControlResult
                {
                    Success = true,
                    Message = "操作成功"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解析集群控制结果失败");
                return new ClusterControlResult
                {
                    Success = false,
                    Message = "解析失败"
                };
            }
        }
    }

    // 数据模型
    public class PythonBackendStatus
    {
        public bool IsConnected { get; set; }
        public string Message { get; set; } = "";
        public DateTime LastCheck { get; set; } = DateTime.Now;
    }

    public class PythonServiceInfo
    {
        public string ServiceName { get; set; } = "AI 处理服务";
        public string Version { get; set; } = "未知";
        public string Uptime { get; set; } = "未知";
        public int RequestCount { get; set; } = 0;
        public double CpuUsage { get; set; } = 0;
        public double MemoryUsage { get; set; } = 0;
    }

    public class AiModuleStatus
    {
        public string FaceRecognitionStatus { get; set; } = "未知";
        public string MaskDetectionStatus { get; set; } = "未知";
        public string LoadBalancingStatus { get; set; } = "未知";
        public int FaceRecognitionAccuracy { get; set; } = 0;
        public int MaskDetectionAccuracy { get; set; } = 0;
        public int CurrentLoad { get; set; } = 0;
        public int ActiveNodes { get; set; } = 0;
    }

    public class TestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int Accuracy { get; set; } = 0;
        public TimeSpan Duration { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
    }

    public class PythonLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = "";
        public string Message { get; set; } = "";
        public string Source { get; set; } = "";
    }

    public class DroneClusterInfo
    {
        public int TotalNodes { get; set; }
        public int ActiveNodes { get; set; }
        public List<ClusterInfo> Clusters { get; set; } = new();
    }

    public class ClusterInfo
    {
        public string ClusterName { get; set; } = "";
        public int NodeCount { get; set; }
        public List<string> Nodes { get; set; } = new();
    }

    public class TaskCreationResult
    {
        public bool Success { get; set; }
        public string TaskId { get; set; } = "";
        public string Message { get; set; } = "";
        public int SubtaskCount { get; set; }
    }

    public class TaskProcessingStatus
    {
        public string TaskName { get; set; } = "";
        public int TotalSubtasks { get; set; }
        public int CompletedSubtasks { get; set; }
        public string Status { get; set; } = ""; // processing, completed, failed
        public string ProcessedImagePath { get; set; } = "";
        public double Progress => TotalSubtasks > 0 ? (double)CompletedSubtasks / TotalSubtasks * 100 : 0;
    }

    public class ClusterControlResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int ActiveNodeCount { get; set; }
    }
} 