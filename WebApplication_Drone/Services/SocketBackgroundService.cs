using WebApplication_Drone.Services;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

public class SocketBackgroundService : BackgroundService
{
    private readonly SocketService _socketService;
    private readonly MissionSocketService _missionsocketService;

    private readonly SqlserverService _sqlserverService;

    public SocketBackgroundService(SocketService socketService, SqlserverService sqlserverService, MissionSocketService missionsocketService)
    {
        _socketService = socketService;
        _missionsocketService = missionsocketService;
        _sqlserverService = sqlserverService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 启动SocketService
        await _socketService.ConnectAsync("192.168.31.35", 5007);

        await _missionsocketService.StartAsync(5009);
        // 启动SqlserverService
        _sqlserverService.run();
    }
}