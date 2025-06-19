using ClassLibrary_Core.Common;
using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Message;
using ClassLibrary_Core.Mission;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace WebApplication.Service
{
    public class SocketService : ISocketService
    {
        private List<byte> _cumulativeBuffer = new List<byte>();
        private ConcurrentQueue<byte[]> _sendQueue = new ConcurrentQueue<byte[]>();
        private SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private const int MaxQueueSize = 1000;
        private TcpClient _client;
        private NetworkStream _stream;
        private bool _isReconnecting;
        private bool _autoReconnect = true;
        private int _maxRetries = 5;
        private int _currentRetry = 0;
        private int _retryInterval = 30000; // 30秒
        private string _currentHost;
        private int _currentPort;

        private readonly ITaskDataService _taskDataService;
        private readonly IDroneService _droneService;
        private readonly ILogger<SocketService> _logger;
        private readonly System.Timers.Timer _timer;

        public event EventHandler<DroneChangedEventArgs>? DroneChanged;
        public event EventHandler<TaskChangedEventArgs>? TaskChanged;

        public SocketService(ITaskDataService taskDataService, IDroneService droneService, ILogger<SocketService> logger)
        {
            _taskDataService = taskDataService;
            _taskDataService.TaskChanged += OnTaskChanged;
            _droneService = droneService;
            _droneService.DroneChanged += OnDroneChanged;
            _logger = logger;

            _timer = new System.Timers.Timer(5000);
            _timer.Elapsed += async (s, e) => await FetchFromSocketAsync();
            _timer.AutoReset = true;
            _timer.Start();
        }

        public async Task ConnectAsync(string host = "192.168.31.35", int port = 5007)
        {
            _currentHost = host;
            _currentPort = port;
            await TryConnectAsync(host, port);
        }

        private async Task TryConnectAsync(string host, int port)
        {
            while (_autoReconnect && _currentRetry < _maxRetries)
            {
                try
                {
                    _isReconnecting = true;
                    Disconnect();

                    _client = new TcpClient();
                    await _client.ConnectAsync(host, port);
                    _stream = _client.GetStream();
                    
                    var message = new Message_Send { content = "30", type = "start_all" };
                    await SendMessageAsync(message);
                    
                    _currentRetry = 0;
                    _isReconnecting = false;
                    
                    _ = Task.Run(() => ReceiveMessagesAsync());
                    _logger.LogInformation("Socket连接已建立");
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Socket连接失败: {Message}", ex.Message);
                    _currentRetry++;
                    await Task.Delay(_retryInterval);
                }
            }
            _isReconnecting = false;
            
            if (_currentRetry >= _maxRetries)
            {
                _logger.LogError("Socket连接重试次数超过最大限制");
            }
        }

        public async Task SendMessageAsync(object message)
        {
            if (_sendQueue.Count >= MaxQueueSize)
            {
                _logger.LogWarning("发送队列已满，丢弃新消息");
                return;
            }

            var data = SerializeMessage(message);
            _sendQueue.Enqueue(data);
            await ProcessSendQueue();
        }

        private async Task ProcessSendQueue()
        {
            await _sendLock.WaitAsync();
            try
            {
                while (_sendQueue.TryDequeue(out byte[] data))
                {
                    if (!IsConnected())
                    {
                        await TryReconnectAsync();
                        if (!IsConnected())
                        {
                            _sendQueue.Enqueue(data);
                            break;
                        }
                    }

                    try
                    {
                        await _stream.WriteAsync(data, 0, data.Length);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "发送失败: {Message}", ex.Message);
                        Disconnect();
                        _sendQueue.Enqueue(data);
                        break;
                    }
                }
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task TryReconnectAsync()
        {
            if (_isReconnecting) return;
            _isReconnecting = true;

            try
            {
                await TryConnectAsync(_currentHost, _currentPort);
                if (IsConnected())
                {
                    await ProcessSendQueue();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重连失败: {Message}", ex.Message);
            }
            finally
            {
                _isReconnecting = false;
            }
        }

        public bool IsConnected()
        {
            return _client?.Connected == true && _stream != null;
        }

        private byte[] SerializeMessage(object message)
        {
            var json = JsonSerializer.Serialize(message);
            var data = Encoding.UTF8.GetBytes(json);
            var length = BitConverter.GetBytes(data.Length);
            return length.Concat(data).ToArray();
        }

        private async Task ReceiveMessagesAsync()
        {
            var buffer = new byte[4096];
            try
            {
                while (IsConnected())
                {
                    var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        _logger.LogWarning("连接被服务器关闭");
                        break;
                    }

                    _cumulativeBuffer.AddRange(buffer.Take(bytesRead));

                    while (TryParseMessageFromBuffer(out JsonDocument? document, out int bytesConsumed))
                    {
                        _cumulativeBuffer.RemoveRange(0, bytesConsumed);
                        
                        if (document != null)
                        {
                            var message = JsonSerializer.Deserialize<Message>(document);
                            if (message != null)
                            {
                                await ProcessMessageAsync(message);
                            }
                            document.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "接收消息时发生错误: {Message}", ex.Message);
            }
            finally
            {
                Disconnect();
            }
        }

        private bool TryParseMessageFromBuffer(out JsonDocument? document, out int bytesConsumed)
        {
            document = null;
            bytesConsumed = 0;

            if (_cumulativeBuffer.Count < 4) return false;

            var lengthBytes = _cumulativeBuffer.Take(4).ToArray();
            var messageLength = BitConverter.ToInt32(lengthBytes, 0);

            if (_cumulativeBuffer.Count < 4 + messageLength) return false;

            var messageBytes = _cumulativeBuffer.Skip(4).Take(messageLength).ToArray();
            bytesConsumed = 4 + messageLength;

            try
            {
                var json = Encoding.UTF8.GetString(messageBytes);
                document = JsonDocument.Parse(json);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解析消息失败");
                return false;
            }
        }

        private async Task ProcessMessageAsync(Message message)
        {
            try
            {
                switch (message.type)
                {
                    case "cluster_info":
                        await HandleClusterInfoAsync(message);
                        break;
                    case "ans_node_info":
                        await HandleAnsNodeInfoAsync(message);
                        break;
                    case "tasks_info":
                        await HandleTasksInfoAsync(message);
                        break;
                    case "subtasks_info":
                        await HandleSubtasksInfoAsync(message);
                        break;
                    case "reassig_info":
                        await HandleReassignInfoAsync(message);
                        break;
                    default:
                        _logger.LogWarning("未知消息类型: {Type}", message.type);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理消息时发生错误: {Message}", ex.Message);
            }
        }

        private async Task HandleClusterInfoAsync(Message message)
        {
            if (message.content is JsonElement contentElement)
            {
                try
                {
                    var content = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(contentElement);
                    var drones = ParseDronesFromJson(content);
                    await _droneService.BulkUpdateDronesAsync(drones);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "解析集群信息失败");
                }
            }
        }

        private async Task HandleAnsNodeInfoAsync(Message message)
        {
            // 处理节点信息响应
            _logger.LogInformation("收到节点信息响应");
        }

        private async Task HandleTasksInfoAsync(Message message)
        {
            if (message.content is JsonElement contentElement)
            {
                try
                {
                    var content = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(contentElement);
                    await ProcessTasksContent(content);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "解析任务信息失败");
                }
            }
        }

        private async Task HandleSubtasksInfoAsync(Message message)
        {
            if (message.content is JsonElement contentElement)
            {
                try
                {
                    var content = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(contentElement);
                    await ProcessSubTasksContent(content);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "解析子任务信息失败");
                }
            }
        }

        private async Task HandleReassignInfoAsync(Message message)
        {
            if (message.content is JsonElement contentElement)
            {
                try
                {
                    var content = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(contentElement);
                    await ProcessReassignContent(content);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "解析重分配信息失败");
                }
            }
        }

        private List<Drone> ParseDronesFromJson(Dictionary<string, JsonElement> content)
        {
            var drones = new List<Drone>();
            
            if (content.ContainsKey("drones"))
            {
                try
                {
                    if (content["drones"].ValueKind == JsonValueKind.Array)
                    {
                        foreach (var droneElement in content["drones"].EnumerateArray())
                        {
                            try
                            {
                                var drone = JsonSerializer.Deserialize<Drone>(droneElement);
                                if (drone != null)
                                {
                                    drones.Add(drone);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "解析单个无人机数据失败");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "解析无人机列表失败");
                }
            }

            return drones;
        }

        private async Task ProcessTasksContent(Dictionary<string, JsonElement> content)
        {
            // 实现任务内容处理逻辑
            _logger.LogInformation("处理任务信息");
        }

        private async Task ProcessSubTasksContent(Dictionary<string, JsonElement> content)
        {
            // 实现子任务内容处理逻辑
            _logger.LogInformation("处理子任务信息");
        }

        private async Task ProcessReassignContent(Dictionary<string, JsonElement> content)
        {
            // 实现重新分配内容处理逻辑
            _logger.LogInformation("处理重新分配信息");
        }

        private async Task FetchFromSocketAsync()
        {
            try
            {
                if (IsConnected())
                {
                    var message = new Message_Send 
                    { 
                        content = "fetch_data", 
                        type = "data_request" 
                    };
                    await SendMessageAsync(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "定时获取数据失败: {Message}", ex.Message);
            }
        }

        private void OnDroneChanged(object? sender, DroneChangedEventArgs e)
        {
            DroneChanged?.Invoke(this, e);
            
            // 向远程服务器发送无人机状态变更
            _ = Task.Run(async () =>
            {
                try
                {
                    var message = new Message_Send
                    {
                        type = "drone_status_update",
                        content = JsonSerializer.Serialize(new
                        {
                            action = e.Action,
                            drone = e.Drone
                        })
                    };
                    await SendMessageAsync(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "发送无人机状态更新失败");
                }
            });
        }

        private void OnTaskChanged(object? sender, TaskChangedEventArgs e)
        {
            TaskChanged?.Invoke(this, e);
            
            // 向远程服务器发送任务状态变更
            _ = Task.Run(async () =>
            {
                try
                {
                    var message = new Message_Send
                    {
                        type = "task_status_update",
                        content = JsonSerializer.Serialize(new
                        {
                            action = e.Action,
                            mainTask = e.MainTask,
                            subTask = e.SubTask
                        })
                    };
                    await SendMessageAsync(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "发送任务状态更新失败");
                }
            });
        }

        public async Task SendFileAsync(string filePath)
        {
            if (!IsConnected())
            {
                throw new InvalidOperationException("未连接到服务器");
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("文件不存在");
            }

            try
            {
                var fileBytes = await File.ReadAllBytesAsync(filePath);
                var fileName = Path.GetFileName(filePath);
                
                var message = new Message_Send
                {
                    type = "file_upload",
                    content = Convert.ToBase64String(fileBytes),
                    // 可以添加文件名等元数据
                };

                await SendMessageAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送文件失败: {FilePath}", filePath);
                throw;
            }
        }

        public void Disconnect()
        {
            try
            {
                _timer?.Stop();
                _stream?.Close();
                _stream?.Dispose();
                _client?.Close();
                _client?.Dispose();
                _cumulativeBuffer.Clear();
                
                // 清空发送队列
                while (_sendQueue.TryDequeue(out _)) { }
                
                _logger.LogInformation("Socket连接已断开");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "断开连接时发生错误");
            }
        }

        public void Dispose()
        {
            Disconnect();
            _sendLock?.Dispose();
        }
    }
} 