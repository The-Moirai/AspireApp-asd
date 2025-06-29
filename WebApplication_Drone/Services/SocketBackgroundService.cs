using WebApplication_Drone.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using WebApplication_Drone.Services.Clean;

public class SocketBackgroundService : BackgroundService
{
    private readonly SocketService _socketService;
    private readonly MissionSocketService _missionsocketService;
    private readonly TaskService _taskService;
    private readonly ILogger<SocketBackgroundService> _logger;

    public SocketBackgroundService(SocketService socketService, MissionSocketService missionsocketService, TaskService taskService, ILogger<SocketBackgroundService> logger)
    {
        _socketService = socketService;
        _missionsocketService = missionsocketService;
        _taskService = taskService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("正在启动 SocketBackgroundService...");
            
            // 只加载任务数据，图片元数据按需加载
            _logger.LogInformation("加载任务数据...");
            await _taskService.LoadTasksFromDatabaseAsync();
            _logger.LogInformation("图片数据将按需从数据库实时加载");
            
            // 启动MissionSocketService (图片接收服务)
            _logger.LogInformation("启动 MissionSocketService 在端口 5009...");
            await _missionsocketService.StartAsync(5009);
            
            // 启动SocketService (连接到Linux端)
            _logger.LogInformation("连接到 Linux 端 192.168.31.35:5007...");
            await _socketService.ConnectAsync("192.168.31.35", 5007);
            
            _logger.LogInformation("所有服务启动完成，SocketBackgroundService 正在运行");
            
            // 保持服务运行直到取消
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SocketBackgroundService 正常关闭");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SocketBackgroundService 启动失败: {Message}", ex.Message);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("正在停止 SocketBackgroundService...");
        
        try
        {
            // 停止 MissionSocketService
            _missionsocketService.Stop();
            _logger.LogInformation("MissionSocketService 已停止");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止 MissionSocketService 时发生错误");
        }
        
        await base.StopAsync(cancellationToken);
    }
}