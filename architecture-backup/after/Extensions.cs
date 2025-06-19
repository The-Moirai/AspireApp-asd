using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.Hosting;

// Adds common .NET Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // 添加全局异常处理
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();

        // 添加内存缓存
        builder.Services.AddMemoryCache();
        
        // 添加数据保护
        builder.Services.AddDataProtection();

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    /// <summary>
    /// 添加业务服务配置
    /// </summary>
    public static TBuilder AddBusinessServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // 配置选项模式
        builder.Services.Configure<DroneServiceOptions>(builder.Configuration.GetSection("DroneService"));
        builder.Services.Configure<SocketServiceOptions>(builder.Configuration.GetSection("SocketService"));
        builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection("Database"));

        // 添加验证
        builder.Services.AddOptionsWithValidateOnStart<DroneServiceOptions>()
            .BindConfiguration("DroneService")
            .ValidateDataAnnotations();

        return builder;
    }

    /// <summary>
    /// 添加分布式缓存配置
    /// </summary>
    public static TBuilder AddDistributedCaching<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Redis分布式缓存
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = builder.Configuration.GetConnectionString("cache");
            options.InstanceName = "AspireApp";
        });

        // 添加缓存服务
        builder.Services.AddScoped<ICacheService, RedisCacheService>();

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    // 添加自定义业务指标
                    .AddMeter("AspireApp.DroneService")
                    .AddMeter("AspireApp.TaskService");
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation()
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation()
                    // 添加自定义业务追踪
                    .AddSource("AspireApp.DroneService")
                    .AddSource("AspireApp.TaskService");
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"])
            // 添加SQL Server健康检查
            .AddSqlServer(
                builder.Configuration.GetConnectionString("app-db") ?? throw new InvalidOperationException("Database connection string not found"),
                name: "sqlserver",
                tags: ["ready", "db"])
            // 添加Redis健康检查
            .AddRedis(
                builder.Configuration.GetConnectionString("cache") ?? throw new InvalidOperationException("Redis connection string not found"),
                name: "redis",
                tags: ["ready", "cache"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks("/health");

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks("/alive", new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });

            // Ready check for all external dependencies
            app.MapHealthChecks("/ready", new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("ready")
            });
        }

        return app;
    }
}

/// <summary>
/// 全局异常处理器
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "An unhandled exception occurred");

        var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An error occurred",
            Detail = httpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment() 
                ? exception.Message 
                : "An internal server error occurred"
        };

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}

// 配置选项类
public class DroneServiceOptions
{
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);
    public int CacheExpirationMinutes { get; set; } = 10;
    public bool EnableRealTimeUpdates { get; set; } = true;
}

public class SocketServiceOptions
{
    public string DefaultHost { get; set; } = "192.168.31.35";
    public int DefaultPort { get; set; } = 5007;
    public int MaxRetries { get; set; } = 5;
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(30);
    public bool AutoReconnect { get; set; } = true;
    public int MaxQueueSize { get; set; } = 1000;
}

public class DatabaseOptions
{
    public int CommandTimeoutSeconds { get; set; } = 30;
    public bool EnableRetryOnFailure { get; set; } = true;
    public int MaxRetryCount { get; set; } = 3;
    public bool EnableSensitiveDataLogging { get; set; } = false;
}
