using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace WebApplication.Data
{
    public class DatabaseInitializationService : BackgroundService
    {
        private readonly IHost _host;
        private readonly ILogger<DatabaseInitializationService> _logger;
        private bool _isInitialized = false;

        public DatabaseInitializationService(IHost host, ILogger<DatabaseInitializationService> logger)
        {
            _host = host;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                if (!_isInitialized)
                {
                    _logger.LogInformation("Starting database initialization...");
                    await DatabaseInitializer.InitializeDatabaseAsync(_host);
                    _isInitialized = true;
                    _logger.LogInformation("Database initialization completed successfully.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while initializing the database.");
                throw;
            }
        }
    }
} 