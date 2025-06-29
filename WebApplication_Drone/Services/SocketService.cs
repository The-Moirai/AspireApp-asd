using ClassLibrary_Core.Common;
using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Message;
using ClassLibrary_Core.Mission;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebApplication_Drone.Services.Models;
using WebApplication_Drone.Services.Clean;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace WebApplication_Drone.Services
{
    public class SocketService
    {
        private List<byte> _cumulativeBuffer = new List<byte>(); // 累积缓冲区
        private ConcurrentQueue<byte[]> _sendQueue = new ConcurrentQueue<byte[]>();// 发送队列
        private SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);// 发送锁
        private const int MaxQueueSize = 1000; // 队列最大容量
        private TcpClient? _client;
        private NetworkStream? _stream;
        private bool _isReconnecting;      // 是否正在重连
        private bool _autoReconnect = true; // 是否启用自动重连
        private int _maxRetries = 5;       // 最大重试次数
        private int _currentRetry = 0;     // 当前重试次数
        private int _retryInterval = 30; // 重试间隔（毫秒）
        private string? _currentHost;
        private int _currentPort;

        private readonly TaskService _taskService;
        private readonly DroneService _droneService;
        private readonly ILogger<SocketService> _logger;
        private readonly System.Timers.Timer _timer;

        public SocketService(TaskService taskService, DroneService droneService, ILogger<SocketService> logger)
        {
            _taskService = taskService;
            _taskService.TaskChanged += OnTaskChanged; // 订阅任务变更事件
            _droneService = droneService;
            _droneService.DroneChanged += OnDroneChanged; // 订阅无人机变更事件
            _logger = logger;

            _timer = new System.Timers.Timer(5000);
            _timer.Elapsed += async (s, e) => await FetchFromSocketAsync();
            _timer.AutoReset = true;
            _timer.Start();
        }


        ///<summary>
        /// 启动 SocketService
        /// </summary>
        /// <param name="host">连接ip地址</param>
        /// <param name="port">连接端口</param>
        /// <returns></returns>
        private async Task TryConnectAsync(string host = "192.168.31.35", int port = 5007)
        {
            while (_autoReconnect && _currentRetry < _maxRetries)
            {
                try
                {
                    _isReconnecting = true;
                    // 清理旧连接
                    Disconnect();
                    // 创建新连接
                    _client = new TcpClient();
                    await _client.ConnectAsync(host, port);
                    _stream = _client.GetStream();
                    Message_Send message = new Message_Send() { content = "30", type = "start_all" };
                    SendMessageAsync(message);
                    _currentRetry = 0; // 重置重试次数
                    _isReconnecting = false;
                    _ = Task.Run(() => ReceiveMessagesAsync()); // 开始接收消息
                    _logger.LogInformation("Socket_TcpClient was bulid");
                    return; // 连接成功，退出循环
                }
                catch (Exception ex)
                {
                    _logger.LogError("Socket_TcpClient was Bad: {Message}", ex.Message);
                    _currentRetry++;
                    await Task.Delay(_retryInterval); // 等待后重试
                }
            }
            _isReconnecting = false;
            // 重连失败处理
            if (_currentRetry >= _maxRetries)
            {
                _logger.LogError("Socket_TcpClient bulid was more than Max");
                

            }
        }
        /// <summary>
        /// 发送文件
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task SendFileAsync(string filePath)
        {
            if (_stream == null) throw new InvalidOperationException("未连接到服务器");
        }
        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="drone"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task SendMessageAsync(Message_Send message)
        {
            if (_sendQueue.Count >= MaxQueueSize)
            {
                _logger.LogWarning("发送队列已满，丢弃新消息");
                return;
            }

            var data = SerializeMessage(message);
            _sendQueue.Enqueue(data);
            await ProcessSendQueue(); // 触发发送


        }
        /// <summary>
        /// 处理发送队列
        /// </summary>
        /// <returns></returns>
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
                            _sendQueue.Enqueue(data); // 重新入队
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
                        _sendQueue.Enqueue(data); // 重新入队
                        break;
                    }
                }
            }
            finally
            {
                _sendLock.Release();
            }
        }
        /// <summary>
        /// 连接到服务器
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public async Task ConnectAsync(string host, int port)
        {
            _currentHost = host;
            _currentPort = port;
            await TryConnectAsync(host, port);
        }
        /// <summary>
        /// 重连逻辑
        /// </summary>
        /// <returns></returns>
        private async Task TryReconnectAsync()
        {
            if (_isReconnecting) return;
            _isReconnecting = true;

            try
            {
                await TryConnectAsync(_currentHost, _currentPort);
                if (IsConnected())
                {
                    await ProcessSendQueue(); // 重连后继续发送
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重连失败: {Message}", ex.Message);
                throw new NotImplementedException("当前网络存在问题，请在解决问题后重试");
            }
            finally
            {
                _isReconnecting = false;
            }
        }
        /// <summary>
        /// 检查连接状态
        /// </summary>
        /// <returns></returns>
        public bool IsConnected()
        {
            return _client?.Connected == true && _stream?.CanWrite == true;
        }
        /// <summary>
        /// 序列化消息
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private byte[] SerializeMessage(Message_Send message)
        {
            var serialized = JsonSerializer.Serialize(message);
            byte[] payload = Encoding.UTF8.GetBytes(serialized);
            byte[] header = BitConverter.GetBytes((uint)payload.Length);
            return header.Concat(payload).ToArray();
        }
        /// <summary>
        /// 接收消息
        /// </summary>
        private async Task ReceiveMessagesAsync()
        {
            var buffer = new byte[20480];
            while (_client.Connected)
            {
                try
                {
                    var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        // 服务器主动断开
                        _logger.LogInformation("连接已断开");
                        if (_autoReconnect && !_isReconnecting)
                        {
                            _ = Task.Run(() => TryConnectAsync(_client.Client.RemoteEndPoint.ToString(), ((System.Net.IPEndPoint)_client.Client.RemoteEndPoint).Port));
                        }
                        break;
                    }
                    // 将新数据追加到累积缓冲区
                    _cumulativeBuffer.AddRange(buffer.Take(bytesRead));

                    // 尝试解析累积缓冲区中的所有完整消息
                    while (TryParseMessageFromBuffer(out var document, out int bytesConsumed))
                    {
                        // 提取完整JSON对应的字节
                        byte[] jsonBytes = _cumulativeBuffer.Take(bytesConsumed).ToArray();
                        _cumulativeBuffer.RemoveRange(0, bytesConsumed);

                        // 反序列化为消息对象
                        string json = Encoding.UTF8.GetString(jsonBytes);
                        _logger.LogDebug("接收到消息: {Json}", json);
                        var message = JsonSerializer.Deserialize<Message>(json);
                        await ProcessMessage(message);

                        // 释放JsonDocument资源
                        document?.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "接收消息时出错: {Message}", ex.Message);
                    break;
                }
            }
        }
        /// <summary>
        /// 尝试从累积缓冲区解析完整的 JSON 消息
        /// </summary>
        /// <param name="document"></param>
        /// <param name="bytesConsumed"></param>
        /// <returns></returns>
        private bool TryParseMessageFromBuffer(out JsonDocument? document, out int bytesConsumed)
        {
            document = null;
            bytesConsumed = 0;

            try
            {
                // 将累积缓冲区数据转为 ReadOnlySpan<byte>
                ReadOnlySpan<byte> bufferSpan = _cumulativeBuffer.ToArray();

                // 尝试解析JSON
                var reader = new Utf8JsonReader(bufferSpan, isFinalBlock: false, default);
                if (JsonDocument.TryParseValue(ref reader, out document))
                {
                    // 计算已解析的字节长度
                    bytesConsumed = (int)reader.BytesConsumed;
                    return true;
                }
            }
            catch (JsonException)
            {
                // JSON不完整或格式错误，继续等待数据
            }

            return false;
        }
        /// <summary>
        /// 处理接收到的消息
        /// </summary>
        /// <param name="message"></param>
        private async Task ProcessMessage(Message message)
        {
            if (message == null)
            {
                _logger.LogWarning("收到空消息，忽略处理");
                return;
            }
            try
            {
                switch (message.type)
                {
                    case "ans_node_info":
                        HandleAnsNodeInfo(message.content);
                        break;
                    case "tasks_info":
                        await HandleTasksInfo(message.content);
                        break;
                    case "Subtasks_info":
                        HandleSubtasksInfo(message.content);
                        break;
                    case "start_success":
                        HandleAnsNodeInfo(message.content);
                        break;
                    case "node_info":
                        HandleAnsNodeInfo(message.content);
                        break;
                    case "cluster_info":
                        HandleClusterInfo(message);
                        break;
                    case "reassign_info":
                        HandleReassig_info(message.content);
                        break;


                    default:
                        _logger.LogWarning("未知消息类型: {MessageType}", message.type);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理消息时发生错误: {Message}", ex.Message);
            }
        }
        /// <summary>
        /// 处理重新分配信息
        /// </summary>
        /// <param name="content"></param>
        private void HandleReassig_info(Dictionary<string, List<object>> content)
        {
            if (!content.TryGetValue("old_node_name", out var oldNodeList) ||
                !content.TryGetValue("subtask_name", out var subTaskList) ||
                !content.TryGetValue("task_name", out var taskList) ||
                !content.TryGetValue("new_node_name", out var newNodeList))
            {
                _logger.LogWarning("reassign_info 消息字段不完整，忽略处理");
                return;
            }

            int count = new[] { oldNodeList.Count, subTaskList.Count, taskList.Count, newNodeList.Count }.Min();

            for (int i = 0; i < count; i++)
            {
                string oldNode = (oldNodeList[i] as JsonElement?)?.GetString() ?? "未知";
                string subtaskDesc = (subTaskList[i] as JsonElement?)?.GetString() ?? "未知";
                string taskDesc = (taskList[i] as JsonElement?)?.GetString() ?? "未知";
                string newNode = (newNodeList[i] as JsonElement?)?.GetString() ?? "未知";

                _logger.LogInformation("任务 '{TaskDesc}' 的子任务 '{SubtaskDesc}' 已从节点 '{OldNode}' 重新分配到节点 '{NewNode}'", taskDesc, subtaskDesc, oldNode, newNode);

                // 1. 同步到 TaskDataService
                var mainTask = _taskService.GetTasks().FirstOrDefault(t => t.Description == taskDesc);
                if (mainTask == null) continue;
                var subTask = mainTask.SubTasks.FirstOrDefault(st => st.Description == subtaskDesc);
                if (subTask == null) continue;

                // 卸载并重装
                _taskService.UnloadSubTask(mainTask.Id, subTask.Id);
                _taskService.ReloadSubTask(mainTask.Id, subTask.Id, newNode);

                // 2. 同步到 DroneDataService
                // 从旧无人机移除子任务
                var oldDrone = _droneService.GetDrones().FirstOrDefault(d => d.Name == oldNode);
                oldDrone?.AssignedSubTasks.RemoveAll(st => st.Id == subTask.Id);

                // 添加到新无人机
                var newDrone = _droneService.GetDrones().FirstOrDefault(d => d.Name == newNode);
                if (newDrone != null && !newDrone.AssignedSubTasks.Any(st => st.Id == subTask.Id))
                {
                    newDrone.AssignedSubTasks.Add(subTask);
                }
            }
        }
        /// <summary>
        /// 处理任务分配信息 - 将子任务分配给具体的无人机节点
        /// content格式: { "节点名": ["子任务名1", "子任务名2", ...] }
        /// </summary>
        /// <param name="content"></param>
        private async Task HandleTasksInfo(Dictionary<string, List<object>> content)
        {
            foreach (var nodeAssignment in content)
            {
                string nodeName = nodeAssignment.Key; // 无人机节点名
                var assignedSubTasks = nodeAssignment.Value; // 分配给该节点的子任务列表

                _logger.LogInformation("为节点 '{NodeName}' 分配了 {Count} 个子任务", nodeName, assignedSubTasks.Count);

                foreach (var subTaskObj in assignedSubTasks)
                {
                    string subTaskName = subTaskObj.ToString();
                    
                    try
                    {
                        // 解析子任务名称: MainTaskName_GroupIndex_SubTaskIndex
                        var parts = subTaskName.Split('_');
                        if (parts.Length < 3)
                        {
                            _logger.LogWarning("子任务名称格式错误: {SubTaskName}", subTaskName);
                            continue;
                        }

                        // 重构主任务名称 (去掉最后两个部分: _GroupIndex_SubTaskIndex)
                        string mainTaskName = string.Join("_", parts.Take(parts.Length - 2));
                        
                        // 通过描述查找主任务
                        if (!Guid.TryParse(mainTaskName, out var mainTaskGuid))
                        {
                            _logger.LogWarning("主任务名称不是有效的GUID格式: {MainTaskName}", mainTaskName);
                            continue;
                        }
                        
                        var mainTask = _taskService.GetTasks().FirstOrDefault(t => t.Id == mainTaskGuid);
                        if (mainTask == null)
                        {
                            _logger.LogWarning("MainTask Not Found 未找到主任务: {MainTaskName} (GUID: {MainTaskGuid})", mainTaskName, mainTaskGuid);
                            continue;
                        }

                        // 查找子任务
                        var subTask = mainTask.SubTasks.FirstOrDefault(st => st.Description == subTaskName);
                        if (subTask == null)
                        {
                            _logger.LogWarning("k在主任务 '{MainTaskName}' 中未找到子任务: {SubTaskName}.", mainTaskName, subTaskName);
                        }

                        // 【关键补全】分配子任务到节点
                        subTask.AssignedDrone = nodeName;
                        subTask.AssignedTime = DateTime.Now;

                        // 异步更新数据库
                        await _taskService.UpdateSubTaskAsync(subTask);

                        // 分配子任务到节点（如有其它业务逻辑）
                        bool success = _taskService.AssignSubTask(mainTask.Id, subTask.Id, nodeName);
                        if (success)
                        {
                            _logger.LogInformation("成功将子任务 '{SubTaskName}' 分配给节点 '{NodeName}'", subTaskName, nodeName);
                        }
                        else
                        {
                            _logger.LogError("分配子任务 '{SubTaskName}' 到节点 '{NodeName}' 失败", subTaskName, nodeName);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "处理子任务分配时出错: {SubTaskName} -> {NodeName}", subTaskName, nodeName);
                    }
                }
            }
        }
        /// <summary>
        /// 处理子任务创建信息 - 为已存在的主任务添加子任务
        /// content格式: { "组名(MainTaskName_GroupIndex)": ["子任务名1", "子任务名2", ...] }
        /// 注意：主任务必须已经存在，此方法不会自动创建主任务
        /// </summary>
        /// <param name="content"></param>
        private void HandleSubtasksInfo(Dictionary<string, List<object>> content)
        {
            foreach (KeyValuePair<string, List<object>> groupInfo in content)
            {
                string groupName = groupInfo.Key; // 组名: MainTaskName_GroupIndex
                var subTaskNames = groupInfo.Value; // 该组的子任务名称列表

                try
                {
                    // 解析组名获取主任务名称
                    var groupParts = groupName.Split('_');
                    if (groupParts.Length < 2)
                    {
                        _logger.LogWarning("组名格式错误: {GroupName}", groupName);
                        continue;
                    }

                    // 重构主任务名称 (去掉最后一个部分: _GroupIndex)
                    string mainTaskName = string.Join("_", groupParts.Take(groupParts.Length - 1));

                    // 查找已存在的主任务
                    if (!Guid.TryParse(mainTaskName, out var mainTaskGuid))
                    {
                        _logger.LogError("主任务名称不是有效的GUID格式: {MainTaskName}，无法添加子任务", mainTaskName);
                        continue;
                    }
                    
                    var mainTask = _taskService.GetTask(mainTaskGuid);
                    if (mainTask == null)
                    {
                        _logger.LogError("Not Found MainTask ,the name is: {MainTaskName} (GUID: {MainTaskGuid})，So the subTask is not add。", mainTaskName, mainTaskGuid);
                        continue;
                    }

                    // 为该组创建子任务
                    foreach (var subTaskObj in subTaskNames)
                    {
                        string subTaskName = subTaskObj.ToString();
                        
                        // 检查子任务是否已存在
                        if (mainTask.SubTasks.Any(st => st.Description == subTaskName))
                        {
                            _logger.LogDebug("子任务已存在: {SubTaskName}", subTaskName);
                            continue;
                        }

                        // 创建新的子任务
                        var subTask = new SubTask
                        {
                            Id = Guid.NewGuid(),
                            Description = subTaskName,
                            Status = TaskStatus.WaitingForActivation,
                            CreationTime = DateTime.Now,
                            ParentTask = mainTask.Id,
                            ReassignmentCount = 0
                        };

                        // 添加子任务到主任务
                        _taskService.addSubTasks(mainTask.Id, subTask);
                        _logger.LogInformation("For MainTaskName :'{MainTaskName}' Add SubTaskName: {SubTaskName} (ID: {SubTaskId})", 
                            mainTaskName, subTaskName, subTask.Id);
                    }

                    _logger.LogInformation("为主任务 '{MainTaskName}' 的组 '{GroupName}' 创建了 {Count} 个子任务", 
                        mainTaskName, groupName, subTaskNames.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理子任务组信息时出错: {GroupName}", groupName);
                }
            }
        }
        /// <summary>
        /// 处理无人机信息
        /// </summary>
        /// <param name="content"></param>
        private void HandleAnsNodeInfo(Dictionary<string, List<object>> content)
        {
            List<Drone> drones = ParseDronesFromJson(content);
            _droneService.SetDrones(drones);
        }
        /// <summary>
        /// 处理簇信息
        /// </summary>
        /// <param name="message"></param>
        private void HandleClusterInfo(Message message)
        {
            foreach (var item in message.content)
            {
                foreach (var cluster in item.Value)
                {
                    string cluster1 = cluster.ToString();
                }
            }
        }

        /// <summary>
        /// 解析无人机信息
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public List<Drone> ParseDronesFromJson(Dictionary<string, List<object>> content)
        {
            // 提取各属性
            var nodesName = content["nodes_name"].ConvertAll(x => ((JsonElement)x).GetString());
            var dealSpeed = content["deal_speed"].ConvertAll(x => ((JsonElement)x).GetDouble());
            var radius = content["radius"].ConvertAll(x => ((JsonElement)x).GetDouble());
            var memory = content["memory"].ConvertAll(x => ((JsonElement)x).GetDouble());
            var leftBandwidth = content["left_bandwidth"].ConvertAll(x => ((JsonElement)x).GetDouble());
            var x = content["x"].ConvertAll(x => ((JsonElement)x).GetDouble());
            var y = content["y"].ConvertAll(x => ((JsonElement)x).GetDouble());
            var cpuUsedRate = content["cpu_used_rate"].ConvertAll(x => ((JsonElement)x).GetDouble());

            // 创建 Drone 对象列表
            var drones = new List<Drone>();

            for (int i = 0; i < nodesName.Count; i++)
            {
                drones.Add(new Drone
                {
                    Id = Guid.NewGuid(), // 使用Guid.NewGuid()生成新的唯一标识符
                    Name = nodesName[i].ToString(),
                    Status = DroneStatus.Idle, // 默认状态
                    CurrentPosition = new GPSPosition(x[i], y[i]),
                    cpu_used_rate = cpuUsedRate[i],
                    memory = memory[i],
                    left_bandwidth = leftBandwidth[i],
                });
            }
                
            return drones;
        }
        /// <summary>
        /// 定时从SocketService获取最新无人机数据
        /// </summary>
        private async Task FetchFromSocketAsync()
        {
            try
            {
                SendMessageAsync(new Message_Send() { content = "", type = "node_info" });
            }
            catch (Exception ex)
            {
                // 可用日志记录异常
            }
        }
        /// <summary>
        /// 无人机变更事件处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnDroneChanged(object? sender, DroneChangedEventArgs e)
        {
            if (e.Action == "Delete")
            {
                // 删除无人机或离线的处理逻辑
                _logger.LogInformation($"Drone {e.Drone.Name} {e.Action}。");
                SendMessageAsync(new Message_Send
                {
                    content = e.Drone.Name,
                    type = "shutdown"
                });
            }
            else if (e.Action == "Update")
            {
                // 更新无人机的处理逻辑
                _logger.LogInformation($"Drone {e.Drone.Name} update Status {e.Drone.Status}。");
            }
            else if (e.Action == "Add")
            {
                // 添加无人机的处理逻辑
                _logger.LogInformation($"Drone was add {e.Drone.Name} 。");
            }
            else
            {
                _logger.LogDebug($"Unknown drone action: {e.Action} for drone {e.Drone.Name}");
            }
        }
        /// <summary>
        /// 无人机变更事件处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnTaskChanged(object? sender, TaskChangedEventArgs e)
        {
            if (e.Action == "Delete")
            {
                // 删除任务的处理逻辑
                _logger.LogInformation($"Task {e.MainTask.Description} was Delete。");
            }
            else if (e.Action == "Update")
            {
                // 更新任务的处理逻辑
                _logger.LogInformation($"Task {e.MainTask.Description} update。");
            }
            else if (e.Action == "Added")
            {
                SendMessageAsync(new Message_Send
                {
                    content = e.MainTask.Description,
                    type = "create_tasks",
                    next_node = e.MainTask.Id.ToString()
                });
                // 添加任务的处理逻辑
                _logger.LogInformation($"new Task {e.MainTask.Description} Add。");
            }
            else
            { }

        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        public void Disconnect()
        {
            _stream?.Close();
            _client?.Close();
        }

    }
}
