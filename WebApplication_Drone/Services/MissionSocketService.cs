using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ClassLibrary_Core.Message;

namespace WebApplication_Drone.Services
{
    public class MissionSocketService
    {
        private readonly ManualResetEvent _stopEvent = new ManualResetEvent(false);//
        private TcpListener _listener;
        private readonly List<TcpClient> _clients = new();
        private readonly TaskDataService _taskDataService;
        private readonly DroneDataService _droneDataService;
        private readonly SqlserverService _sqlserverService;
        private readonly ILogger<MissionSocketService> _logger;
        private readonly string _imageBasePath;

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
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _logger.LogInformation("MissionSocketService started on port {Port}", port);

            while (!_stopEvent.WaitOne(0))
            {
                var client = await _listener.AcceptTcpClientAsync();
                _logger.LogInformation("Client connected");
                _clients.Add(client);
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        /// <summary>
        /// 处理客户端连接
        /// </summary>
        /// <param name="client">客户端连接</param>
        private async Task HandleClientAsync(TcpClient client)
        {
            var stream = client.GetStream();
            
            try
            {
                // 读取JSON消息头
                var (jsonMessage, remainingData) = await ReadJsonMessageFromStream(stream);
                if (string.IsNullOrEmpty(jsonMessage))
                {
                    _logger.LogDebug("未能读取到JSON消息头，连接可能已关闭");
                    return;
                }

                _logger.LogInformation("接收到JSON消息: {MessageJson}", jsonMessage);
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                };
                
                var message = JsonSerializer.Deserialize<MessageFromNode>(jsonMessage, options);
                if (message != null)
                {
                    _logger.LogInformation("成功解析消息类型: {Type}", message.type);
                    
                    // 根据消息类型进行处理
                    switch (message.type)
                    {
                        case "single_image":
                            _logger.LogInformation("🖼️ 开始处理single_image消息，剩余数据: {RemainingBytes} 字节", remainingData?.Length ?? 0);
                            await ProcessSingleImageWithHeader(message, stream, remainingData);
                            break;
                        case "image_data":
                            _logger.LogInformation("📦 开始处理image_data消息，剩余数据: {RemainingBytes} 字节", remainingData?.Length ?? 0);
                            await ProcessImageDataDirect(message, stream, remainingData);
                            // image_data是旧协议，现在已废弃，处理完后关闭连接
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
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON解析失败");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling client: {Message}", ex.Message);
            }
            finally
            {
                _clients.Remove(client);
                client.Close();
                _logger.LogInformation("Client disconnected");
            }
        }

        /// <summary>
        /// 从流中读取JSON消息，返回JSON消息和剩余的二进制数据
        /// </summary>
        private async Task<(string jsonMessage, byte[] remainingData)> ReadJsonMessageFromStream(NetworkStream stream)
        {
            var buffer = new byte[4096];
            var jsonBuffer = new List<byte>();
            
            _logger.LogDebug("开始读取JSON消息...");
            
            while (true)
            {
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    _logger.LogDebug("流读取结束，总字节: {TotalBytes}", jsonBuffer.Count);
                    break; // 连接关闭
                }

                _logger.LogDebug("读取到 {BytesRead} 字节，累积 {TotalBytes} 字节", bytesRead, jsonBuffer.Count + bytesRead);
                jsonBuffer.AddRange(buffer.Take(bytesRead));
                
                // 尝试解析JSON
                if (TryParseJsonFromBuffer(jsonBuffer, out var jsonMessage, out int bytesConsumed))
                {
                    _logger.LogDebug("成功解析JSON，消耗 {BytesConsumed} 字节", bytesConsumed);
                    
                    // 跳过JSON内容
                    int totalConsumed = bytesConsumed;
                    
                    // 跳过换行符分隔符（如果存在）
                    if (totalConsumed < jsonBuffer.Count && jsonBuffer[totalConsumed] == (byte)'\n')
                    {
                        totalConsumed++; // 跳过换行符
                        _logger.LogDebug("跳过JSON后的换行符分隔符");
                    }
                    
                    // 返回JSON消息和剩余的二进制数据
                    var remainingData = jsonBuffer.Skip(totalConsumed).ToArray();
                    if (remainingData.Length > 0)
                    {
                        _logger.LogDebug("JSON解析后还有 {RemainingBytes} 字节剩余数据", remainingData.Length);
                    }
                    
                    return (jsonMessage, remainingData);
                }
                
                // 如果累积的数据太多仍然无法解析JSON，可能是协议错误
                if (jsonBuffer.Count > 10000) // 10KB限制
                {
                    _logger.LogError("JSON消息过大或格式错误，累积字节: {BufferSize}", jsonBuffer.Count);
                    
                    // 查找是否有换行符
                    int newlinePos = -1;
                    for (int i = 0; i < Math.Min(jsonBuffer.Count, 1000); i++)
                    {
                        if (jsonBuffer[i] == (byte)'\n')
                        {
                            newlinePos = i;
                            break;
                        }
                    }
                    
                    if (newlinePos >= 0)
                    {
                        _logger.LogError("发现换行符在位置 {NewlinePos}，但JSON解析失败。前100字符: {Content}", 
                            newlinePos, Encoding.UTF8.GetString(jsonBuffer.Take(Math.Min(100, newlinePos)).ToArray()));
                    }
                    else
                    {
                        _logger.LogError("未找到换行符分隔符，前500字符: {Content}", 
                            Encoding.UTF8.GetString(jsonBuffer.Take(500).ToArray()));
                    }
                    
                    break;
                }
            }
            
            return (string.Empty, new byte[0]);
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
        private async Task ProcessSingleImageWithHeader(MessageFromNode message, NetworkStream stream, byte[] remainingData = null)
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
                        
                        if (!string.IsNullOrEmpty(imagePath) || imageId > 0)
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
                            
                            _logger.LogInformation("✅ 单张图片接收成功: TaskId={TaskId}, SubTask={SubTask}, ImagePath={ImagePath}, ImageId={ImageId}, 序号={ImageIndex}/{TotalImages}", 
                                taskId, subtaskName, imagePath, imageId, imageIndex, totalImages);
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
        private async Task ProcessImageDataDirect(MessageFromNode message, NetworkStream stream, byte[] preloadedData = null)
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
        private async Task<(string imagePath, long imageId)> SaveImageToDatabase(NetworkStream stream, string taskId, string subtaskName, int imageIndex, string fileName, long fileSize, byte[] preloadedData = null)
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
                        
                        return (imagePath, 0); // 返回文件路径但数据库ID为0
                    }
                }

                // 保存图片到数据库
                long imageId = 0;
                try
                {
                    imageId = await _sqlserverService.SaveSubTaskImageAsync(subTaskId, imageData, fileName, imageIndex, $"子任务 {subtaskName} 的处理结果图片");
                    _logger.LogInformation("图片保存到数据库成功: SubTaskId={SubTaskId}, ImageId={ImageId}, FileName={FileName}, Size={Size}字节", 
                        subTaskId, imageId, fileName, imageData.Length);
                    
                    // 同步更新TaskDataService中的SubTask.Images集合
                    await SyncImageToTaskDataService(taskGuid, subTaskId, imageId, fileName, imageIndex, imageData.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "保存图片到数据库失败，将使用文件系统备份方案");
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

                return (imagePathFinal, imageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存图片失败");
                throw;
            }
        }

        /// <summary>
        /// 同步图片信息到TaskDataService中的SubTask.Images集合
        /// </summary>
        private async Task SyncImageToTaskDataService(Guid taskGuid, Guid subTaskId, long imageId, string fileName, int imageIndex, long fileSize)
        {
            try
            {
                // 从数据库获取完整的图片信息
                var imageData = await _sqlserverService.GetSubTaskImageAsync(imageId);
                if (imageData != null)
                {
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
                                subTask.Images.Add(imageData);
                                
                                // 按图片序号排序
                                subTask.Images = subTask.Images.OrderBy(img => img.ImageIndex).ThenBy(img => img.UploadTime).ToList();
                                
                                _logger.LogDebug("✅ 同步图片到TaskDataService成功: SubTaskId={SubTaskId}, ImageId={ImageId}, FileName={FileName}", 
                                    subTaskId, imageId, fileName);
                            }
                            else
                            {
                                _logger.LogDebug("图片已存在，跳过同步: ImageId={ImageId}", imageId);
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
                else
                {
                    _logger.LogWarning("❌ 无法从数据库获取图片数据进行同步: ImageId={ImageId}", imageId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 同步图片到TaskDataService失败: ImageId={ImageId}", imageId);
            }
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
        private async Task<string> SaveImageFromHeaderInfo(NetworkStream stream, string taskId, string subtaskName, int imageIndex, string fileName, long fileSize, byte[] preloadedData = null)
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
            _stopEvent.Set(); // 触发循环退出
            foreach (var client in _clients)
            {
                client.Close();
            }
            _clients.Clear();
            _listener?.Stop();
            _logger.LogInformation("MissionSocketService stopped");
        }
    }
}

