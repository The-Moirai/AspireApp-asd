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
        private readonly ILogger<MissionSocketService> _logger;
        private readonly string _imageBasePath;

        public MissionSocketService(TaskDataService taskDataService, DroneDataService droneDataService, ILogger<MissionSocketService> logger)
        {
            _taskDataService = taskDataService;
            _droneDataService = droneDataService;
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
            var buffer = new byte[4096];
            try
            {
                while (client.Connected)
                {
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        try
                        {
                            // 尝试解析JSON消息
                            var messageJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            _logger.LogDebug("接收到消息: {MessageJson}", messageJson);
                            
                            // 检查是否为有效的JSON
                            if (IsValidJson(messageJson))
                            {
                                var message = JsonSerializer.Deserialize<MessageFromNode>(messageJson);
                                if (message != null)
                                {
                                    await ProcessMessage(message, stream);
                                }
                            }
                            else
                            {
                                // 如果不是JSON，可能是直接的图片数据流
                                _logger.LogWarning("接收到非JSON数据，可能是图片流");
                            }
                        }
                        catch (JsonException)
                        {
                            // JSON解析失败，可能是图片数据
                            _logger.LogWarning("JSON解析失败，跳过此数据包");
                        }
                    }
                }
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
        /// 检查字符串是否为有效的JSON
        /// </summary>
        private bool IsValidJson(string jsonString)
        {
            try
            {
                JsonDocument.Parse(jsonString);
                return true;
            }
            catch (JsonException)
            {
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
                case "image_data":
                    await ProcessImageData(message, stream);
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
                var subtaskName = message.content["subtask_name"]?.ToString();
                if (!string.IsNullOrEmpty(subtaskName))
                {
                    var taskId = Guid.Parse(subtaskName.Split("_")[0]);
                    _taskDataService.CompleteSubTask(taskId, subtaskName);
                    _logger.LogInformation("子任务完成: {SubtaskName}", subtaskName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理任务信息失败");
            }
        }

        /// <summary>
        /// 处理图片数据
        /// </summary>
        private async Task ProcessImageData(MessageFromNode message, NetworkStream stream)
        {
            try
            {
                var subtaskName = message.content["subtask_name"]?.ToString();
                var taskId = message.content["task_id"]?.ToString();
                var imageCount = 1; // 默认单张图片
                
                // 检查是否包含图片数量信息
                if (message.content.ContainsKey("image_count"))
                {
                    int.TryParse(message.content["image_count"]?.ToString(), out imageCount);
                }
                
                if (!string.IsNullOrEmpty(subtaskName) && !string.IsNullOrEmpty(taskId))
                {
                    var imagePaths = new List<string>();
                    
                    // 接收多张图片
                    for (int i = 0; i < imageCount; i++)
                    {
                        var imagePath = await SaveImageToFile(stream, taskId, subtaskName, i + 1);
                        if (!string.IsNullOrEmpty(imagePath))
                        {
                            imagePaths.Add(imagePath);
                        }
                    }
                    
                    if (imagePaths.Any())
                    {
                        // 批量更新任务数据，添加图片路径
                        if (imagePaths.Count == 1)
                        {
                            _taskDataService.UpdateSubTaskImage(Guid.Parse(taskId), subtaskName, imagePaths[0]);
                        }
                        else
                        {
                            _taskDataService.AddSubTaskImages(Guid.Parse(taskId), subtaskName, imagePaths);
                        }
                        
                        _logger.LogInformation("图片批量保存成功: TaskId={TaskId}, SubTask={SubTask}, 图片数量={ImageCount}", 
                            taskId, subtaskName, imagePaths.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理图片数据失败");
            }
        }

        /// <summary>
        /// 处理任务结果
        /// </summary>
        private async Task ProcessTaskResult(MessageFromNode message)
        {
            try
            {
                var subtaskName = message.content["subtask_name"]?.ToString();
                var result = message.content["result"]?.ToString();
                var taskId = message.content["task_id"]?.ToString();
                
                if (!string.IsNullOrEmpty(subtaskName) && !string.IsNullOrEmpty(taskId))
                {
                    _taskDataService.UpdateSubTaskResult(Guid.Parse(taskId), subtaskName, result);
                    _logger.LogInformation("任务结果更新: {SubtaskName} - {Result}", subtaskName, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理任务结果失败");
            }
        }

        /// <summary>
        /// 保存图片文件
        /// </summary>
        private async Task<string> SaveImageToFile(NetworkStream stream, string taskId, string subtaskName, int imageIndex)
        {
            try
            {
                // 接收文件名长度 (4字节，Python使用struct.pack('I'))
                var fileNameLengthBuffer = new byte[4];
                await ReadExactBytes(stream, fileNameLengthBuffer, 4);
                int fileNameLength = BitConverter.ToInt32(fileNameLengthBuffer, 0);

                // 接收文件名
                var fileNameBuffer = new byte[fileNameLength];
                await ReadExactBytes(stream, fileNameBuffer, fileNameLength);
                string fileName = Encoding.UTF8.GetString(fileNameBuffer);

                // 接收文件大小 (8字节，Python使用struct.pack('Q'))
                var fileSizeBuffer = new byte[8];
                await ReadExactBytes(stream, fileSizeBuffer, 8);
                long fileSize = BitConverter.ToInt64(fileSizeBuffer, 0);

                // 创建任务专用文件夹
                var taskImagePath = Path.Combine(_imageBasePath, taskId);
                Directory.CreateDirectory(taskImagePath);

                // 生成唯一文件名
                var fileExtension = Path.GetExtension(fileName);
                var uniqueFileName = $"{subtaskName}_{DateTime.Now:yyyyMMddHHmmss}_{imageIndex}{fileExtension}";
                var savePath = Path.Combine(taskImagePath, uniqueFileName);

                // 保存文件
                using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write))
                {
                    var buffer = new byte[4096];
                    long totalBytesReceived = 0;

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
                    }
                }

                // 返回相对于wwwroot的路径，用于Web访问
                var webPath = $"/TaskImages/{taskId}/{uniqueFileName}";
                _logger.LogInformation("图片保存完成: {SavePath}, Web路径: {WebPath}, 文件大小: {FileSize}", 
                    savePath, webPath, fileSize);
                
                return webPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存图片文件失败");
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
