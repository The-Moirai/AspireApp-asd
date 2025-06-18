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
        private string ImagePath;
        private readonly TaskDataService _taskDataService;
        private readonly DroneDataService _droneDataService;
        private readonly ILogger<MissionSocketService> _logger;
        public MissionSocketService(TaskDataService taskDataService, DroneDataService droneDataService, ILogger<MissionSocketService> logger)
        {
            _taskDataService = taskDataService;
            _droneDataService = droneDataService;
            _logger = logger;
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
                        var messageJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        _logger.LogDebug("接收到消息: {MessageJson}", messageJson);
                        // 反序列化 JSON 为 Mission 对象
                        var message = JsonSerializer.Deserialize<MessageFromNode>(messageJson);
                        if (message != null && message.type == "task_info")
                        {
                            _taskDataService.CompleteSubTask(Guid.Parse(((string)(message.content["subtask_name"])).Split("_")[0]), message.content["subtask_name"]);
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

       

        private async void SaveImageToFile(Stream _stream,string file)
        {
            // 接收文件名长度
            var fileNameLengthBuffer = new byte[4];
            await _stream.ReadAsync(fileNameLengthBuffer, 0, fileNameLengthBuffer.Length);
            int fileNameLength = BitConverter.ToInt32(fileNameLengthBuffer, 0);

            // 接收文件名
            var fileNameBuffer = new byte[fileNameLength];
            await _stream.ReadAsync(fileNameBuffer, 0, fileNameBuffer.Length);
            string fileName = Encoding.UTF8.GetString(fileNameBuffer);

            // 接收文件大小
            var fileSizeBuffer = new byte[8];
            await _stream.ReadAsync(fileSizeBuffer, 0, fileSizeBuffer.Length);
            long fileSize = BitConverter.ToInt64(fileSizeBuffer, 0);

            // 准备接收文件内容
            //string savePath = Path.Combine(_config.ImageFolderPath+file, fileName);
            //Directory.CreateDirectory(_config.ImageFolderPath+file); // 确保保存目录存在

            //using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write))
            //{
            //    var buffer = new byte[4096];
            //    long totalBytesReceived = 0;

            //    while (totalBytesReceived < fileSize)
            //    {
            //        int bytesToRead = (int)Math.Min(buffer.Length, fileSize - totalBytesReceived);
            //        int bytesRead = await _stream.ReadAsync(buffer, 0, bytesToRead);
            //        if (bytesRead == 0) break;

            //        await fileStream.WriteAsync(buffer, 0, bytesRead);
            //        totalBytesReceived += bytesRead;
            //    }
            //}
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
