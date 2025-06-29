using AspireApp_Drone.BlazorApp_Drone.Hubs;
using ClassLibrary_Core.Drone;
using Microsoft.AspNetCore.SignalR;
using System.Net.Http;
using System.Text.Json;

namespace BlazorApp_Web.Service
{
    // DronePushBackgroundService.cs
    public class DronePushBackgroundService : BackgroundService
    {
        private readonly IHubContext<DroneHub> _hubContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DronePushBackgroundService> _logger;
        private List<Drone> _lastDrones = new();
        private DateTime _lastSuccessfulUpdate = DateTime.MinValue;
        private int _consecutiveErrors = 0;
        private const int MaxConsecutiveErrors = 5;

        public DronePushBackgroundService(IHubContext<DroneHub> hubContext,
                                        IHttpClientFactory httpClientFactory,
                                            ILogger<DronePushBackgroundService> logger)
        {
            _hubContext = hubContext;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var drones = await GetRealTimeDronesAsync();

                    // 只有当数据有变化时才推送
                    if (HasDronesChanged(drones))
                    {
                        await _hubContext.Clients.All.SendAsync("ReceiveDronesPosition", drones, cancellationToken: stoppingToken);
                        _lastDrones = drones?.ToList() ?? new List<Drone>();
                        _lastSuccessfulUpdate = DateTime.Now;
                        _consecutiveErrors = 0;
                        _logger.LogDebug("推送了 {Count} 个无人机的数据", drones?.Count ?? 0);
                    }
                }
                catch (Exception ex)
                {
                    _consecutiveErrors++;
                    _logger.LogError(ex, "推送无人机数据时发生异常 (连续错误次数: {ErrorCount})", _consecutiveErrors);
                    
                    // 如果连续错误过多，增加延迟时间
                    if (_consecutiveErrors >= MaxConsecutiveErrors)
                    {
                        _logger.LogWarning("连续错误次数过多，将延长推送间隔");
                        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                        continue;
                    }
                }

                // 动态调整推送间隔
                var delay = CalculateDelay();
                await Task.Delay(delay, stoppingToken);
            }
        }

        private async Task<List<Drone>?> GetRealTimeDronesAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("ApiService");
                client.Timeout = TimeSpan.FromSeconds(10); // 设置较短的超时时间
                
                // 使用新的实时数据API
                var response = await client.GetAsync("api/drones/realtime");
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    
                    // 直接解析JSON，不使用强类型
                    using var jsonDoc = JsonDocument.Parse(jsonString);
                    var root = jsonDoc.RootElement;
                    
                    // 检查是否有Success属性（新格式）
                    if (root.TryGetProperty("success", out var successElement))
                    {
                        var success = successElement.GetBoolean();
                        if (!success)
                        {
                            var message = root.TryGetProperty("Message", out var messageElement) 
                                ? messageElement.GetString() 
                                : "未知错误";
                            _logger.LogWarning("实时数据API返回失败: {Message}", message);
                            return null;
                        }
                        
                        // 检查Data属性
                        if (root.TryGetProperty("data", out var dataElement))
                        {
                            var drones = JsonSerializer.Deserialize<List<Drone>>(dataElement.GetRawText(), new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                            
                            if (drones != null)
                            {
                                // 获取额外的元数据
                                var count = root.TryGetProperty("count", out var countElement) ? countElement.GetInt32() : drones.Count;
                                var dataSource = root.TryGetProperty("dataSource", out var sourceElement) ? sourceElement.GetString() : "Unknown";
                                var timestamp = root.TryGetProperty("timestamp", out var timeElement) ? timeElement.GetString() : "Unknown";
                                
                                _logger.LogDebug("成功获取实时无人机数据，数量: {Count}, 数据源: {DataSource}, 时间戳: {Timestamp}", 
                                    count, dataSource, timestamp);
                                return drones;
                            }
                        }
                    }
                    else
                    {
                        // 兼容旧格式：直接检查Data属性
                        if (root.TryGetProperty("Data", out var dataElement))
                        {
                            var drones = JsonSerializer.Deserialize<List<Drone>>(dataElement.GetRawText(), new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                            
                            if (drones != null)
                            {
                                _logger.LogDebug("成功获取实时无人机数据（旧格式），数量: {Count}", drones.Count);
                                return drones;
                            }
                        }
                        else
                        {
                            // 尝试直接解析为Drone列表（最简格式）
                            var drones = JsonSerializer.Deserialize<List<Drone>>(jsonString, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                            
                            if (drones != null)
                            {
                                _logger.LogDebug("成功获取实时无人机数据（直接格式），数量: {Count}", drones.Count);
                                return drones;
                            }
                        }
                    }
                    
                    _logger.LogWarning("实时数据API返回的数据格式不正确");
                    return null;
                }
                else
                {
                    _logger.LogWarning("实时数据API请求失败，状态码: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "无法连接到实时数据API服务");
                return null;
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogWarning("实时数据API请求超时");
                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "解析实时数据API响应失败");
                return null;
            }
        }

        private bool HasDronesChanged(List<Drone>? newDrones)
        {
            if (newDrones == null) return false;
            
            // 如果是第一次获取数据
            if (!_lastDrones.Any()) return newDrones.Any();
            
            // 数量不同
            if (_lastDrones.Count != newDrones.Count) return true;
            
            // 检查ID集合是否相同
            var lastIds = _lastDrones.Select(d => d.Id).ToHashSet();
            var newIds = newDrones.Select(d => d.Id).ToHashSet();
            if (!lastIds.SetEquals(newIds)) return true;
            
            // 检查位置和状态变化
            foreach (var newDrone in newDrones)
            {
                var lastDrone = _lastDrones.FirstOrDefault(d => d.Id == newDrone.Id);
                if (lastDrone != null)
                {
                    if (lastDrone.Status != newDrone.Status ||
                        Math.Abs(lastDrone.CurrentPosition?.Latitude_x - newDrone.CurrentPosition?.Latitude_x ?? 0) > 0.1 ||
                        Math.Abs(lastDrone.CurrentPosition?.Longitude_y - newDrone.CurrentPosition?.Longitude_y ?? 0) > 0.1 ||
                        Math.Abs(lastDrone.cpu_used_rate - newDrone.cpu_used_rate) > 0.05)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }

        private TimeSpan CalculateDelay()
        {
            // 基础延迟5秒
            var baseDelay = TimeSpan.FromSeconds(5);
            
            // 如果有错误，增加延迟
            if (_consecutiveErrors > 0)
            {
                return TimeSpan.FromSeconds(5 + _consecutiveErrors * 2);
            }
            
            // 如果长时间没有更新，减少推送频率
            if (DateTime.Now - _lastSuccessfulUpdate > TimeSpan.FromMinutes(5))
            {
                return TimeSpan.FromSeconds(10);
            }
            
            return baseDelay;
        }
    }
}

