using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ClassLibrary_Core.Message;
using ClassLibrary_Core.Data;


namespace WebApplication_Drone.Services
{
    public class MissionSocketService
    {
        private readonly ManualResetEvent _stopEvent = new ManualResetEvent(false);
        private TcpListener? _listener;
        private readonly List<TcpClient> _clients = new();
        private readonly TaskDataService _taskDataService;
        private readonly DroneDataService _droneDataService;
        private readonly SqlserverService _sqlserverService;
        private readonly ILogger<MissionSocketService> _logger;
        private readonly string _imageBasePath;

        // 网络优化配置
        private const int DefaultReceiveTimeout = 120000; // 120秒接收超时
        private const int DefaultSendTimeout = 60000;     // 60秒发送超时
        private const int LargeFileTimeout = 300000;      // 300秒大文件超时
        private const int DefaultBufferSize = 65536;      // 64KB缓冲区
        private const int LargeFileBufferSize = 131072;   // 128KB大文件缓冲区
        private const long LargeFileThreshold = 1048576;  // 1MB大文件阈值
        private const int MaxConcurrentClients = 50;      // 最大并发客户端
        
        // 连接统计
        private volatile int _activeConnections = 0;
        private long _totalBytesReceived = 0;
        private long _totalImagesReceived = 0;

        /// <summary>
        /// 图片保存完成事件
        /// </summary>
        public static event EventHandler<ImageSavedEventArgs>? ImageSaved;

        public MissionSocketService(TaskDataService taskDataService, DroneDataService droneDataService, SqlserverService sqlserverService, ILogger<MissionSocketService> logger)
        {
            _taskDataService = taskDataService;
            _droneDataService = droneDataService;
            _sqlserverService = sqlserverService;
            _logger = logger;
            _imageBasePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "TaskImages");
            Directory.CreateDirectory(_imageBasePath);
        }

        /// <summary>
        /// 启动服务
        /// </summary>
        /// <param name="port">监听的端口号</param>
        public async Task StartAsync(int port)
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                
                // 设置服务器socket选项
                _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, DefaultBufferSize);
                _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, DefaultBufferSize);
                
                _logger.LogInformation("🚀 MissionSocketService 已启动，端口: {Port}", port);
                _logger.LogInformation("⚙️  网络配置: 接收超时={ReceiveTimeout}ms, 发送超时={SendTimeout}ms, 缓冲区={BufferSize}KB", 
                    DefaultReceiveTimeout, DefaultSendTimeout, DefaultBufferSize / 1024);

                // 启动监听任务，不阻塞当前线程
                _ = Task.Run(async () => await AcceptClientsAsync());
                
                // 启动统计任务
                _ = Task.Run(async () => await LogStatisticsAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 启动 MissionSocketService 失败，端口: {Port}", port);
                throw;
            }
        }

        /// <summary>
        /// 定期记录统计信息
        /// </summary>
        private async Task LogStatisticsAsync()
        {
            while (!_stopEvent.WaitOne(0))
            {
                try
                {
                    await Task.Delay(30000); // 每30秒记录一次统计
                    
                    _logger.LogInformation("📊 连接统计: 活跃连接={ActiveConnections}, 总接收字节={TotalBytes:N0}, 总图片数={TotalImages}", 
                        _activeConnections, _totalBytesReceived, _totalImagesReceived);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "记录统计信息失败");
                }
            }
        }

        /// <summary>
        /// 接受客户端连接的异步循环
        /// </summary>
        private async Task AcceptClientsAsync()
        {
            try
            {
                while (!_stopEvent.WaitOne(0))
                {
                    try
                    {
                        // 检查并发连接数限制
                        if (_activeConnections >= MaxConcurrentClients)
                        {
                            _logger.LogWarning("⚠️  达到最大并发连接数限制: {MaxConnections}，等待连接释放", MaxConcurrentClients);
                            await Task.Delay(1000);
                            continue;
                        }

                        var client = await _listener.AcceptTcpClientAsync();
                        Interlocked.Increment(ref _activeConnections);
                        
                        _logger.LogDebug("🔗 客户端连接: {RemoteEndPoint}, 活跃连接数: {ActiveConnections}", 
                            client.Client.RemoteEndPoint, _activeConnections);
                        
                        lock (_clients)
                        {
                            _clients.Add(client);
                        }
                        
                        // 为每个客户端启动独立的处理任务
                        _ = Task.Run(async () => await HandleClientAsync(client));
                    }
                    catch (ObjectDisposedException)
                    {
                        // 服务正在停止，忽略此异常
                        _logger.LogDebug("TcpListener已释放，停止接受新连接");
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ 接受客户端连接时发生错误");
                        // 短暂延迟后继续监听
                        await Task.Delay(1000);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 客户端接受循环发生严重错误");
            }
            finally
            {
                _logger.LogInformation("客户端接受循环已停止");
            }
        }

        /// <summary>
        /// 配置客户端TCP选项
        /// </summary>
        private void ConfigureClientSocket(TcpClient client, long expectedFileSize = 0)
        {
            try
            {
                var socket = client.Client;
                
                // 根据文件大小选择超时和缓冲区配置
                bool isLargeFile = expectedFileSize > LargeFileThreshold;
                int receiveTimeout = isLargeFile ? LargeFileTimeout : DefaultReceiveTimeout;
                int sendTimeout = DefaultSendTimeout;
                int bufferSize = isLargeFile ? LargeFileBufferSize : DefaultBufferSize;
                
                // 设置超时
                socket.ReceiveTimeout = receiveTimeout;
                socket.SendTimeout = sendTimeout;
                
                // 设置缓冲区大小
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, bufferSize);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, bufferSize);
                
                // 设置TCP选项
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                
                // Linux/Windows特定的keepalive设置
                if (OperatingSystem.IsLinux())
                {
                    socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 60);
                    socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 10);
                    socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3);
                }
                
                _logger.LogDebug("⚙️  客户端socket配置: 接收超时={ReceiveTimeout}ms, 缓冲区={BufferSize}KB, 大文件模式={IsLargeFile}", 
                    receiveTimeout, bufferSize / 1024, isLargeFile);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️  配置客户端socket选项失败");
            }
        }

        /// <summary>
        /// 处理客户端连接
        /// </summary>
        /// <param name="client">客户端连接</param>
        private async Task HandleClientAsync(TcpClient client)
        {
            var clientEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
            var connectionStartTime = DateTime.Now;
            
            try
            {
                // 初始配置客户端socket
                ConfigureClientSocket(client);
                
                var stream = client.GetStream();
                
                // 读取JSON消息头
                var (jsonMessage, remainingData) = await ReadJsonMessageFromStreamAsync(stream);
                if (string.IsNullOrEmpty(jsonMessage))
                {
                    _logger.LogDebug("未能读取到JSON消息头，连接可能已关闭: {ClientEndpoint}", clientEndpoint);
                    return;
                }

                _logger.LogDebug("📨 接收到JSON消息: {MessageJson}", jsonMessage);
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                };
                
                var message = JsonSerializer.Deserialize<MessageFromNode>(jsonMessage, options);
                if (message != null)
                {
                    _logger.LogInformation("✅ 成功解析消息类型: {Type} from {ClientEndpoint}", message.type, clientEndpoint);
                    
                    // 根据消息类型进行处理
                    switch (message.type)
                    {
                        case "single_image":
                            _logger.LogInformation("🖼️ 开始处理single_image消息，剩余数据: {RemainingBytes} 字节", remainingData?.Length ?? 0);
                            
                            // 根据文件大小重新配置socket
                            if (message.content.ContainsKey("filesize") && 
                                message.content["filesize"] is JsonElement fileSizeElement && 
                                fileSizeElement.TryGetInt64(out long fileSize))
                            {
                                ConfigureClientSocket(client, fileSize);
                            }
                            
                            await ProcessSingleImageWithHeader(message, stream, remainingData);
                            break;
                            
                        case "image_data":
                            _logger.LogInformation("📦 开始处理image_data消息，剩余数据: {RemainingBytes} 字节", remainingData?.Length ?? 0);
                            await ProcessImageDataDirect(message, stream, remainingData);
                            break;
                            
                        case "task_info":
                        case "task_result":
                            _logger.LogInformation("📋 处理任务消息: {Type}", message.type);
                            await ProcessMessage(message, stream);
                            break;
                            
                        default:
                            _logger.LogWarning("❓ 未知消息类型: {Type}", message.type);
                            await ProcessMessage(message, stream);
                            break;
                    }
                }
                else
                {
                    _logger.LogError("❌ JSON消息解析失败，message为null: {JsonMessage}", jsonMessage);
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                _logger.LogWarning("⏰ 客户端连接超时: {ClientEndpoint}, 连接时长: {Duration:F1}秒", 
                    clientEndpoint, (DateTime.Now - connectionStartTime).TotalSeconds);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "❌ JSON解析失败: {ClientEndpoint}", clientEndpoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 处理客户端连接错误: {ClientEndpoint}, 消息: {Message}", clientEndpoint, ex.Message);
            }
            finally
            {
                // 清理客户端连接
                lock (_clients)
                {
                    _clients.Remove(client);
                }
                
                try
                {
                    client.Close();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "关闭客户端连接时发生错误");
                }
                
                Interlocked.Decrement(ref _activeConnections);
                
                var connectionDuration = (DateTime.Now - connectionStartTime).TotalSeconds;
                _logger.LogDebug("🔌 客户端断开: {ClientEndpoint}, 连接时长: {Duration:F1}秒, 剩余活跃连接: {ActiveConnections}", 
                    clientEndpoint, connectionDuration, _activeConnections);
            }
        }

        /// <summary>
        /// 从流中读取JSON消息，返回JSON消息和剩余的二进制数据（异步版本）
        /// </summary>
        private async Task<(string jsonMessage, byte[] remainingData)> ReadJsonMessageFromStreamAsync(NetworkStream stream)
        {
            var buffer = new byte[DefaultBufferSize];
            var jsonBuffer = new List<byte>();
            var cancellationTokenSource = new CancellationTokenSource(DefaultReceiveTimeout);
            
            _logger.LogDebug("📖 开始读取JSON消息...");
            
            try
            {
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationTokenSource.Token);
                    if (bytesRead == 0)
                    {
                        _logger.LogDebug("流读取结束，总字节: {TotalBytes}", jsonBuffer.Count);
                        break; // 连接关闭
                    }

                    _logger.LogDebug("📖 读取到 {BytesRead} 字节，累积 {TotalBytes} 字节", bytesRead, jsonBuffer.Count + bytesRead);
                    jsonBuffer.AddRange(buffer.Take(bytesRead));
                    
                    // 尝试解析JSON
                    if (TryParseJsonFromBuffer(jsonBuffer, out var jsonMessage, out int bytesConsumed))
                    {
                        // 计算剩余的二进制数据
                        var remainingData = jsonBuffer.Skip(bytesConsumed+1).ToArray();
                        
                        _logger.LogDebug("✅ JSON解析成功，消息长度: {JsonLength}, 剩余数据: {RemainingBytes} 字节", 
                            jsonMessage.Length, remainingData.Length);
                        
                        return (jsonMessage, remainingData);
                    }
                    
                    // 防止缓冲区过大
                    if (jsonBuffer.Count > 1024 * 1024) // 1MB限制
                    {
                        throw new InvalidDataException("JSON消息过大，超过1MB限制");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("⏰ 读取JSON消息超时");
                throw new TimeoutException("读取JSON消息超时");
            }
            
            return (string.Empty, Array.Empty<byte>());
        }

        /// <summary>
        /// 尝试从累积缓冲区解析完整的JSON消息
        /// </summary>
        /// <param name="cumulativeBuffer">累积缓冲区</param>
        /// <param name="jsonMessage">解析出的JSON消息</param>
        /// <param name="bytesConsumed">消耗的字节数</param>
        /// <returns>是否成功解析</returns>
        private bool TryParseJsonFromBuffer(List<byte> cumulativeBuffer, out string jsonMessage, out int bytesConsumed)
        {
            jsonMessage = string.Empty;
            bytesConsumed = 0;

            try
            {
                // 首先查找换行符分隔符，这是我们协议的边界标识
                int newlineIndex = -1;
                for (int i = 0; i < cumulativeBuffer.Count; i++)
                {
                    if (cumulativeBuffer[i] == (byte)'\n')
                    {
                        newlineIndex = i;
                        break;
                    }
                }
                
                if (newlineIndex == -1)
                {
                    // 没有找到换行符，JSON可能不完整
                    _logger.LogDebug("未找到换行符分隔符，等待更多数据");
                    return false;
                }
                
                // 提取到换行符之前的数据作为JSON
                byte[] jsonBytes = cumulativeBuffer.Take(newlineIndex).ToArray();
                string potentialJson = Encoding.UTF8.GetString(jsonBytes);
                
                _logger.LogDebug("尝试解析JSON，换行符位置: {NewlineIndex}, JSON长度: {JsonLength}", newlineIndex, jsonBytes.Length);
                _logger.LogDebug("候选JSON内容: {JsonContent}", potentialJson);
                
                // 验证这是有效的JSON
                try
                {
                    using var document = JsonDocument.Parse(potentialJson);
                    // 如果解析成功，返回结果
                    jsonMessage = potentialJson;
                    bytesConsumed = newlineIndex; // 不包含换行符本身
                    
                    _logger.LogDebug("JSON解析成功，消耗字节: {BytesConsumed}", bytesConsumed);
                    return true;
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogWarning("换行符前的内容不是有效JSON: {Message}, 内容: {Content}", 
                        jsonEx.Message, potentialJson.Length > 100 ? potentialJson.Substring(0, 100) + "..." : potentialJson);
                    
                    // 如果到换行符的内容不是有效JSON，可能是协议错误
                    // 我们跳过这个换行符，继续寻找下一个
                    cumulativeBuffer.RemoveRange(0, newlineIndex + 1);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "JSON解析过程中发生异常");
                return false;
            }
        }

        /// <summary>
        /// 处理接收到的消息
        /// </summary>
        private async Task ProcessMessage(MessageFromNode message, NetworkStream stream)
        {
            switch (message.type)
            {
                case "task_info":
                    await ProcessTaskInfo(message);
                    break;
                case "task_result":
                    await ProcessTaskResult(message);
                    break;
                default:
                    _logger.LogWarning("未知消息类型: {Type}", message.type);
                    break;
            }
        }

        /// <summary>
        /// 处理任务信息
        /// </summary>
        private async Task ProcessTaskInfo(MessageFromNode message)
        {
            try
            {
                var subtaskName = "";
                var taskId = "";
                
                // 处理subtask_name
                if (message.content.ContainsKey("subtask_name"))
                {
                    var subtaskValue = message.content["subtask_name"];
                    if (subtaskValue is JsonElement element && element.ValueKind == JsonValueKind.String)
                    {
                        subtaskName = element.GetString() ?? "";
                    }
                    else
                    {
                        subtaskName = subtaskValue?.ToString() ?? "";
                    }
                }
                
                // 处理task_id
                if (message.content.ContainsKey("task_id"))
                {
                    var taskIdValue = message.content["task_id"];
                    if (taskIdValue is JsonElement element && element.ValueKind == JsonValueKind.String)
                    {
                        taskId = element.GetString() ?? "";
                    }
                    else
                    {
                        taskId = taskIdValue?.ToString() ?? "";
                    }
                }
                
                if (!string.IsNullOrEmpty(subtaskName))
                {
                    // 尝试从子任务名称中提取任务ID（假设格式为 taskId_x_y）
                    var taskIdString = subtaskName.Split("_")[0];
                    if (Guid.TryParse(taskIdString, out var taskGuid))
                    {
                        _taskDataService.CompleteSubTask(taskGuid, subtaskName);
                        _logger.LogInformation("子任务完成: {SubtaskName} (TaskId: {TaskId})", subtaskName, taskGuid);
                    }
                    else
                    {
                        _logger.LogWarning("无法从子任务名称中解析出有效的GUID格式任务ID: {SubtaskName}, 提取的字符串: {TaskIdString}", 
                            subtaskName, taskIdString);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理任务信息失败");
            }
        }

        /// <summary>
        /// 处理单张图片（带头消息）
        /// </summary>
        private async Task ProcessSingleImageWithHeader(MessageFromNode message, NetworkStream stream, byte[]? remainingData = null)
        {
            try
            {
                var subtaskName = "";
                var taskId = "";
                
                // 处理subtask_name
                if (message.content.ContainsKey("subtask_name"))
                {
                    var subtaskValue = message.content["subtask_name"];
                    if (subtaskValue is JsonElement element && element.ValueKind == JsonValueKind.String)
                    {
                        subtaskName = element.GetString() ?? "";
                    }
                    else
                    {
                        subtaskName = subtaskValue?.ToString() ?? "";
                    }
                }
                
                // 处理task_id
                if (message.content.ContainsKey("task_id"))
                {
                    var taskIdValue = message.content["task_id"];
                    if (taskIdValue is JsonElement element && element.ValueKind == JsonValueKind.String)
                    {
                        taskId = element.GetString() ?? "";
                    }
                    else
                    {
                        taskId = taskIdValue?.ToString() ?? "";
                    }
                }
                
                var imageIndex = 1;
                var totalImages = 1;
                var fileName = "";
                long fileSize = 0;

                // 从头消息中获取图片信息
                if (message.content.ContainsKey("image_index"))
                {
                    var imageIndexValue = message.content["image_index"];
                    if (imageIndexValue is JsonElement element && element.ValueKind == JsonValueKind.Number)
                    {
                        imageIndex = element.GetInt32();
                    }
                    else
                    {
                        int.TryParse(imageIndexValue?.ToString(), out imageIndex);
                    }
                }
                
                if (message.content.ContainsKey("total_images"))
                {
                    var totalImagesValue = message.content["total_images"];
                    if (totalImagesValue is JsonElement element && element.ValueKind == JsonValueKind.Number)
                    {
                        totalImages = element.GetInt32();
                    }
                    else
                    {
                        int.TryParse(totalImagesValue?.ToString(), out totalImages);
                    }
                }
                
                if (message.content.ContainsKey("filename"))
                {
                    var fileNameValue = message.content["filename"];
                    if (fileNameValue is JsonElement element && element.ValueKind == JsonValueKind.String)
                    {
                        fileName = element.GetString() ?? "";
                    }
                    else
                    {
                        fileName = fileNameValue?.ToString() ?? "";
                    }
                }
                
                if (message.content.ContainsKey("filesize"))
                {
                    var fileSizeValue = message.content["filesize"];
                    if (fileSizeValue is JsonElement element && element.ValueKind == JsonValueKind.Number)
                    {
                        fileSize = element.GetInt64();
                    }
                    else
                    {
                        long.TryParse(fileSizeValue?.ToString(), out fileSize);
                    }
                }

                _logger.LogInformation("收到single_image消息: TaskId={TaskId}, SubTask={SubTask}, 序号={ImageIndex}/{TotalImages}, 文件名={FileName}, 大小={FileSize}字节", 
                    taskId, subtaskName, imageIndex, totalImages, fileName, fileSize);

                if (!string.IsNullOrEmpty(subtaskName) && !string.IsNullOrEmpty(taskId) && fileSize > 0)
                {
                    try
                    {
                        // 保存图片到数据库和文件系统
                        var (imagePath, imageId) = await SaveImageToDatabase(stream, taskId, subtaskName, imageIndex, fileName, fileSize, remainingData);
                        
                        if (!string.IsNullOrEmpty(imagePath) || imageId != Guid.Empty)
                        {
                            // 更新任务数据，添加图片路径（向后兼容）
                            Guid taskGuid;
                            if (!Guid.TryParse(taskId, out taskGuid))
                            {
                                _logger.LogWarning("TaskId不是有效的GUID格式: {TaskId}，将生成新的GUID", taskId);
                                taskGuid = Guid.NewGuid();
                            }
                            
                            if (!string.IsNullOrEmpty(imagePath))
                            {
                                _taskDataService.UpdateSubTaskImage(taskGuid, subtaskName, imagePath);
                            }
                            
                            // 更新统计信息
                            Interlocked.Increment(ref _totalImagesReceived);
                            Interlocked.Add(ref _totalBytesReceived, fileSize);
                            
                            _logger.LogInformation("✅ 单张图片接收成功: TaskId={TaskId}, SubTask={SubTask}, ImagePath={ImagePath}, ImageId={ImageId}, 序号={ImageIndex}/{TotalImages}, 大小={FileSize:N0}字节", 
                                taskId, subtaskName, imagePath, imageId, imageIndex, totalImages, fileSize);
                        }
                        else
                        {
                            _logger.LogWarning("❌ 单张图片保存失败: TaskId={TaskId}, SubTask={SubTask}, 序号={ImageIndex}", 
                                taskId, subtaskName, imageIndex);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ 接收单张图片失败: TaskId={TaskId}, SubTask={SubTask}, 序号={ImageIndex}", 
                            taskId, subtaskName, imageIndex);
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️  single_image消息参数不完整: TaskId={TaskId}, SubTask={SubTask}, FileSize={FileSize}", 
                        taskId, subtaskName, fileSize);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 处理单张图片头消息失败");
            }
        }

        /// <summary>
        /// 直接处理图片数据（兼容旧版本协议）
        /// 注意：image_data消息只是一个头消息，实际的图片数据会通过后续的single_image消息发送
        /// 警告：此协议已废弃，请使用single_image协议
        /// </summary>
        private async Task ProcessImageDataDirect(MessageFromNode message, NetworkStream stream, byte[]? preloadedData = null)
        {
            _logger.LogWarning("⚠️ image_data协议已废弃，建议使用single_image协议");
            
            try
            {
                var subtaskName = "";
                var taskId = "";
                
                // 处理subtask_name
                if (message.content.ContainsKey("subtask_name"))
                {
                    var subtaskValue = message.content["subtask_name"];
                    if (subtaskValue is JsonElement element && element.ValueKind == JsonValueKind.String)
                    {
                        subtaskName = element.GetString() ?? "";
                    }
                    else
                    {
                        subtaskName = subtaskValue?.ToString() ?? "";
                    }
                }
                
                // 处理task_id
                if (message.content.ContainsKey("task_id"))
                {
                    var taskIdValue = message.content["task_id"];
                    if (taskIdValue is JsonElement element && element.ValueKind == JsonValueKind.String)
                    {
                        taskId = element.GetString() ?? "";
                    }
                    else
                    {
                        taskId = taskIdValue?.ToString() ?? "";
                    }
                }
                
                var imageCount = 0;
                
                _logger.LogInformation("收到image_data类型消息: TaskId={TaskId}, SubTask={SubTask}", taskId, subtaskName);
                
                // 获取图片数量
                if (message.content.ContainsKey("image_count"))
                {
                    var imageCountValue = message.content["image_count"];
                    if (imageCountValue is JsonElement element && element.ValueKind == JsonValueKind.Number)
                    {
                        imageCount = element.GetInt32();
                    }
                    else
                    {
                        int.TryParse(imageCountValue?.ToString(), out imageCount);
                    }
                }
                
                if (!string.IsNullOrEmpty(subtaskName) && !string.IsNullOrEmpty(taskId) && imageCount > 0)
                {
                    _logger.LogInformation("开始接收 {ImageCount} 张图片: TaskId={TaskId}, SubTask={SubTask}", 
                        imageCount, taskId, subtaskName);
                    
                    // image_data消息只是一个头消息，不包含实际的图片数据
                    // 后续的single_image消息会通过HandleClientAsync的循环来处理
                    // 这里只需要记录信息即可
                    _logger.LogDebug("image_data头消息处理完成，等待后续的 {ImageCount} 个single_image消息", imageCount);
                }
                else
                {
                    _logger.LogWarning("image_data消息参数不完整: TaskId={TaskId}, SubTask={SubTask}, ImageCount={ImageCount}", 
                        taskId, subtaskName, imageCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理image_data消息失败");
            }
        }

        /// <summary>
        /// 处理任务结果
        /// </summary>
        private async Task ProcessTaskResult(MessageFromNode message)
        {
            try
            {
                var subtaskName = "";
                var result = "";
                var taskId = "";
                
                // 处理subtask_name
                if (message.content.ContainsKey("subtask_name"))
                {
                    var subtaskValue = message.content["subtask_name"];
                    if (subtaskValue is JsonElement element && element.ValueKind == JsonValueKind.String)
                    {
                        subtaskName = element.GetString() ?? "";
                    }
                    else
                    {
                        subtaskName = subtaskValue?.ToString() ?? "";
                    }
                }
                
                // 处理result
                if (message.content.ContainsKey("result"))
                {
                    var resultValue = message.content["result"];
                    if (resultValue is JsonElement element && element.ValueKind == JsonValueKind.String)
                    {
                        result = element.GetString() ?? "";
                    }
                    else
                    {
                        result = resultValue?.ToString() ?? "";
                    }
                }
                
                // 处理task_id
                if (message.content.ContainsKey("task_id"))
                {
                    var taskIdValue = message.content["task_id"];
                    if (taskIdValue is JsonElement element && element.ValueKind == JsonValueKind.String)
                    {
                        taskId = element.GetString() ?? "";
                    }
                    else
                    {
                        taskId = taskIdValue?.ToString() ?? "";
                    }
                }
                
                if (!string.IsNullOrEmpty(subtaskName) && !string.IsNullOrEmpty(taskId))
                {
                    // 注意：Result 字段已被移除，这里只记录日志信息
                    _logger.LogInformation("收到任务结果信息: {SubtaskName} - {Result} (TaskId: {TaskId})", subtaskName, result, taskId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理任务结果失败");
            }
        }

        /// <summary>
        /// 保存图片到数据库和文件系统
        /// </summary>
        private async Task<(string imagePath, Guid imageId)> SaveImageToDatabase(NetworkStream stream, string taskId, string subtaskName, int imageIndex, string fileName, long fileSize, byte[]? preloadedData = null)
        {
            try
            {
                _logger.LogDebug("开始保存第{ImageIndex}张图片到数据库: {FileName}, {FileSize}字节", imageIndex, fileName, fileSize);

                // 验证文件大小的合理性
                if (fileSize <= 0 || fileSize > 100 * 1024 * 1024) // 100MB限制
                {
                    throw new InvalidDataException($"文件大小异常: {fileSize}");
                }

                // 读取图片数据到内存
                var imageData = new byte[fileSize];
                long totalBytesReceived = 0;

                // 首先复制预加载的数据
                if (preloadedData != null && preloadedData.Length > 0)
                {
                    Array.Copy(preloadedData, 0, imageData, 0, Math.Min(preloadedData.Length, fileSize));
                    totalBytesReceived += preloadedData.Length;
                    _logger.LogDebug("复制预加载数据: {PreloadedBytes} 字节", preloadedData.Length);
                }

                // 继续从流中读取剩余数据
                var buffer = new byte[4096];
                while (totalBytesReceived < fileSize)
                {
                    int bytesToRead = (int)Math.Min(buffer.Length, fileSize - totalBytesReceived);
                    int bytesRead = await stream.ReadAsync(buffer, 0, bytesToRead);
                    if (bytesRead == 0) 
                    {
                        _logger.LogWarning("连接意外断开，已接收字节: {Received}/{Total}", totalBytesReceived, fileSize);
                        break;
                    }

                    Array.Copy(buffer, 0, imageData, totalBytesReceived, bytesRead);
                    totalBytesReceived += bytesRead;
                    
                    // 每接收1MB记录一次进度
                    if (totalBytesReceived % (1024 * 1024) == 0 || totalBytesReceived == fileSize)
                    {
                        _logger.LogDebug("接收进度: {Received}/{Total} ({Percentage:F1}%)", 
                            totalBytesReceived, fileSize, (double)totalBytesReceived / fileSize * 100);
                    }
                }

                // 解析子任务ID - 根据子任务名称从TaskDataService中获取实际的子任务ID
                Guid subTaskId = Guid.Empty;
                if (!Guid.TryParse(taskId, out var taskGuid))
                {
                    _logger.LogWarning("TaskId不是有效的GUID格式: {TaskId}，将生成新的GUID", taskId);
                    taskGuid = Guid.NewGuid();
                }

                // 从数据库中获取实际的子任务ID
                var subTask = await _sqlserverService.GetSubTaskByDescriptionAsync(taskGuid, subtaskName);
                if (subTask != null)
                {
                    subTaskId = subTask.Id;
                    _logger.LogDebug("从数据库找到子任务ID: {SubTaskId} for {SubTaskName}", subTaskId, subtaskName);
                }
                else
                {
                    _logger.LogWarning("数据库中未找到子任务: {SubTaskName} in Task {TaskId}", subtaskName, taskId);
                    
                    // 尝试从内存中查找（向后兼容）
                    var mainTaskFromMemory = _taskDataService.GetTask(taskGuid);
                    if (mainTaskFromMemory != null)
                    {
                        var subTaskFromMemory = mainTaskFromMemory.SubTasks.FirstOrDefault(st => st.Description == subtaskName);
                        if (subTaskFromMemory != null)
                        {
                            subTaskId = subTaskFromMemory.Id;
                            _logger.LogDebug("从内存找到子任务ID: {SubTaskId} for {SubTaskName}", subTaskId, subtaskName);
                        }
                        else
                        {
                            _logger.LogWarning("内存中也未找到子任务: {SubTaskName}，可用子任务: [{AvailableSubTasks}]", 
                                subtaskName, string.Join(", ", mainTaskFromMemory.SubTasks.Select(st => st.Description)));
                        }
                    }
                    
                    // 如果仍然没有找到子任务，保存到文件系统
                    if (subTaskId == Guid.Empty)
                    {
                        _logger.LogInformation("跳过数据库保存，仅保存到文件系统");
                        
                        string imagePath = string.Empty;
                        try
                        {
                            imagePath = await SaveImageToFileSystem(imageData, taskId, subtaskName, imageIndex, fileName);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "保存图片到文件系统失败");
                        }
                        
                        return (imagePath, Guid.Empty); // 返回文件路径但数据库ID为空
                    }
                }

                // 同时保存到文件系统作为备份（向后兼容）
                string imagePathFinal = string.Empty;
                try
                {
                    imagePathFinal = await SaveImageToFileSystem(imageData, taskId, subtaskName, imageIndex, fileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "保存图片到文件系统失败");
                }

                // 保存图片到数据库
                Guid imageId = Guid.Empty;
                try
                {
                    imageId = await _sqlserverService.SaveSubTaskImageAsync(subTaskId, imageData, fileName, imageIndex, $"子任务 {subtaskName} 的处理结果图片");
                    _logger.LogInformation("图片保存到数据库成功: SubTaskId={SubTaskId}, ImageId={ImageId}, FileName={FileName}, Size={Size}字节", 
                        subTaskId, imageId, fileName, imageData.Length);
                    
                    // 同步更新TaskDataService中的SubTask.Images集合
                    await SyncImageToTaskDataService(taskGuid, subTaskId, imageId, fileName, imageIndex, imageData.Length);
                    
                    // 🔥 触发图片保存事件，供其他服务监听
                    ImageSaved?.Invoke(this, new ImageSavedEventArgs
                    {
                        TaskId = taskGuid,
                        SubTaskId = subTaskId,
                        ImageId = imageId,
                        FileName = fileName,
                        ImageIndex = imageIndex,
                        FileSize = imageData.Length,
                        SavedAt = DateTime.Now,
                        WebPath = imagePathFinal,
                        SubTaskName = subtaskName
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "保存图片到数据库失败，将使用文件系统备份方案");
                }
                return (imagePathFinal, imageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存图片失败");
                throw;
            }
        }

        /// <summary>
        /// 同步图片元数据到TaskDataService中的SubTask.Images集合（不包含二进制数据）
        /// </summary>
        private async Task SyncImageToTaskDataService(Guid taskGuid, Guid subTaskId, Guid imageId, string fileName, int imageIndex, long fileSize)
        {
            try
            {
                // 创建轻量级图片元数据对象，不从数据库加载二进制数据
                var imageMetadata = new SubTaskImage
                {
                    Id = imageId,
                    SubTaskId = subTaskId,
                    ImageData = null, // 不加载二进制数据，节省内存
                    FileName = fileName,
                    FileExtension = Path.GetExtension(fileName),
                    FileSize = fileSize,
                    ContentType = GetContentTypeByExtension(Path.GetExtension(fileName)),
                    ImageIndex = imageIndex,
                    UploadTime = DateTime.Now,
                    Description = $"子任务 {subTaskId} 的处理结果图片"
                };

                // 直接更新TaskDataService中的任务数据
                var mainTask = _taskDataService.GetTask(taskGuid);
                if (mainTask != null)
                {
                    var subTask = mainTask.SubTasks.FirstOrDefault(st => st.Id == subTaskId);
                    if (subTask != null)
                    {
                        // 检查是否已存在相同的图片
                        if (!subTask.Images.Any(img => img.Id == imageId))
                        {
                            subTask.Images.Add(imageMetadata);
                            
                            // 按图片序号排序
                            subTask.Images = subTask.Images.OrderBy(img => img.ImageIndex).ThenBy(img => img.UploadTime).ToList();
                            
                            _logger.LogDebug("✅ 同步图片元数据到TaskDataService成功: SubTaskId={SubTaskId}, ImageId={ImageId}, FileName={FileName}", 
                                subTaskId, imageId, fileName);
                        }
                        else
                        {
                            _logger.LogDebug("图片元数据已存在，跳过同步: ImageId={ImageId}", imageId);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ 未找到子任务进行图片同步: SubTaskId={SubTaskId}", subTaskId);
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️ 未找到任务进行图片同步: TaskId={TaskId}", taskGuid);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 同步图片元数据到TaskDataService失败: ImageId={ImageId}", imageId);
            }
        }

        /// <summary>
        /// 根据文件扩展名获取MIME类型
        /// </summary>
        private static string GetContentTypeByExtension(string fileExtension)
        {
            return fileExtension.ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                _ => "image/png" // 默认为PNG
            };
        }

        /// <summary>
        /// 保存图片到文件系统
        /// </summary>
        private async Task<string> SaveImageToFileSystem(byte[] imageData, string taskId, string subtaskName, int imageIndex, string fileName)
        {
            try
            {
                // 创建任务专用文件夹
                var taskImagePath = Path.Combine(_imageBasePath, taskId);
                Directory.CreateDirectory(taskImagePath);

                // 生成唯一文件名
                var fileExtension = Path.GetExtension(fileName);
                var uniqueFileName = $"{subtaskName}_{DateTime.Now:yyyyMMddHHmmss}_{imageIndex}{fileExtension}";
                var savePath = Path.Combine(taskImagePath, uniqueFileName);

                // 保存文件
                await File.WriteAllBytesAsync(savePath, imageData);

                // 返回相对于wwwroot的路径，用于Web访问
                var webPath = $"/TaskImages/{taskId}/{uniqueFileName}";
                _logger.LogInformation("图片保存到文件系统成功: {SavePath}, Web路径: {WebPath}, 文件大小: {FileSize}", 
                    savePath, webPath, imageData.Length);
                
                return webPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存图片到文件系统失败");
                throw;
            }
        }

        /// <summary>
        /// 根据头消息信息保存图片文件（保留向后兼容）
        /// </summary>
        private async Task<string> SaveImageFromHeaderInfo(NetworkStream stream, string taskId, string subtaskName, int imageIndex, string fileName, long fileSize, byte[]? preloadedData = null)
        {
            try
            {
                _logger.LogDebug("开始根据头消息信息保存第{ImageIndex}张图片: {FileName}, {FileSize}字节", imageIndex, fileName, fileSize);

                // 验证文件大小的合理性
                if (fileSize <= 0 || fileSize > 100 * 1024 * 1024) // 100MB限制
                {
                    throw new InvalidDataException($"文件大小异常: {fileSize}");
                }

                // 创建任务专用文件夹
                var taskImagePath = Path.Combine(_imageBasePath, taskId);
                Directory.CreateDirectory(taskImagePath);

                // 生成唯一文件名
                var fileExtension = Path.GetExtension(fileName);
                var uniqueFileName = $"{subtaskName}_{DateTime.Now:yyyyMMddHHmmss}_{imageIndex}{fileExtension}";
                var savePath = Path.Combine(taskImagePath, uniqueFileName);

                _logger.LogDebug("开始接收文件内容到: {SavePath}", savePath);

                // 直接保存文件内容（跳过Python的文件名长度等信息）
                using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write))
                {
                    long totalBytesReceived = 0;

                    // 首先写入预加载的数据
                    if (preloadedData != null && preloadedData.Length > 0)
                    {
                        await fileStream.WriteAsync(preloadedData, 0, preloadedData.Length);
                        totalBytesReceived += preloadedData.Length;
                        _logger.LogDebug("写入预加载数据: {PreloadedBytes} 字节", preloadedData.Length);
                    }

                    // 继续从流中读取剩余数据
                    var buffer = new byte[4096];
                    while (totalBytesReceived < fileSize)
                    {
                        int bytesToRead = (int)Math.Min(buffer.Length, fileSize - totalBytesReceived);
                        int bytesRead = await stream.ReadAsync(buffer, 0, bytesToRead);
                        if (bytesRead == 0) 
                        {
                            _logger.LogWarning("连接意外断开，已接收字节: {Received}/{Total}", totalBytesReceived, fileSize);
                            break;
                        }

                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalBytesReceived += bytesRead;
                        
                        // 每接收1MB记录一次进度
                        if (totalBytesReceived % (1024 * 1024) == 0 || totalBytesReceived == fileSize)
                        {
                            _logger.LogDebug("接收进度: {Received}/{Total} ({Percentage:F1}%)", 
                                totalBytesReceived, fileSize, (double)totalBytesReceived / fileSize * 100);
                        }
                    }
                }

                // 返回相对于wwwroot的路径，用于Web访问
                var webPath = $"/TaskImages/{taskId}/{uniqueFileName}";
                _logger.LogInformation("头消息图片保存完成: {SavePath}, Web路径: {WebPath}, 文件大小: {FileSize}", 
                    savePath, webPath, fileSize);
                
                return webPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "根据头消息信息保存图片文件失败");
                throw;
            }
        }

        /// <summary>
        /// 确保读取指定数量的字节
        /// </summary>
        private async Task ReadExactBytes(NetworkStream stream, byte[] buffer, int count)
        {
            int totalBytesRead = 0;
            while (totalBytesRead < count)
            {
                int bytesRead = await stream.ReadAsync(buffer, totalBytesRead, count - totalBytesRead);
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException($"连接意外断开，期望读取 {count} 字节，实际读取 {totalBytesRead} 字节");
                }
                totalBytesRead += bytesRead;
            }
        }

        /// <summary>
        /// 停止服务
        /// </summary>
        public void Stop()
        {
            try
            {
                _logger.LogInformation("正在停止 MissionSocketService...");
                
                _stopEvent.Set(); // 触发循环退出
                
                // 关闭所有客户端连接
                lock (_clients)
                {
                    foreach (var client in _clients.ToList())
                    {
                        try
                        {
                            client?.Close();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "关闭客户端连接时发生错误");
                        }
                    }
                    _clients.Clear();
                }
                
                // 停止监听器
                try
                {
                    _listener?.Stop();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "停止监听器时发生错误");
                }
                
                _logger.LogInformation("MissionSocketService 已停止");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止 MissionSocketService 时发生错误");
            }
        }
    }
}

