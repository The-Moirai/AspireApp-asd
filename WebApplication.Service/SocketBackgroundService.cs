using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace WebApplication.Service
{
    public class SocketBackgroundService : BackgroundService
    {
        private readonly ILogger<SocketBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public SocketBackgroundService(ILogger<SocketBackgroundService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Socket后台服务已启动");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var socketService = scope.ServiceProvider.GetRequiredService<ISocketService>();

                    // 检查连接状态，如果断开则重连
                    if (!socketService.IsConnected())
                    {
                        _logger.LogInformation("检测到Socket连接断开，正在尝试重连...");
                        await socketService.ConnectAsync("192.168.31.35",5007);
                    }

                    // 定期发送心跳或状态检查
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // 服务正在停止
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Socket后台服务运行时发生错误");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }

            _logger.LogInformation("Socket后台服务已停止");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("正在停止Socket后台服务...");
            
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var socketService = scope.ServiceProvider.GetRequiredService<ISocketService>();
                socketService.Disconnect();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止Socket服务时发生错误");
            }

            await base.StopAsync(cancellationToken);
        }
    }
} 