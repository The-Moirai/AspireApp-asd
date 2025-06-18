using WebApplication_Drone.Services;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

public class SocketBackgroundService : BackgroundService
{
    private readonly SocketService _socketService;
    private readonly SqlserverService _sqlserverService;

    public SocketBackgroundService(SocketService socketService, SqlserverService sqlserverService)
    {
        _socketService = socketService;
        _sqlserverService = sqlserverService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 启动SocketService
        await _socketService.ConnectAsync("192.168.31.35", 5007);
        // 启动SqlserverService
        _sqlserverService.run();
    }
}