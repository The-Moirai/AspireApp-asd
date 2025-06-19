using Microsoft.OpenApi.Models;
using WebApplication.Data;
using WebApplication.Service;
using Microsoft.AspNetCore.Routing;

var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// 配置路由选项
builder.Services.Configure<RouteOptions>(options =>
{
    options.ConstraintMap.Add("string", typeof(string));
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "无人机管理系统 API", Version = "v1" });

    // 添加文件上传支持过滤器
    c.OperationFilter<WebApplication.SwaggerFileOperationFilter>();

    // 添加文件上传支持
    c.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });
});

// Register database service
builder.Services.AddSingleton<IDatabaseService, DatabaseService>();

// Register services - 使用 Singleton 避免生命周期冲突
builder.Services.AddSingleton<IDroneService, DroneService>();
builder.Services.AddSingleton<IMissionService, MissionService>();
builder.Services.AddSingleton<ITaskDataService, TaskDataService>();
builder.Services.AddSingleton<ISocketService, SocketService>();

// 添加后台服务
builder.Services.AddHostedService<SocketBackgroundService>();

// 添加跨域支持
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "无人机管理系统 API v1");
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Initialize services
try
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("应用程序初始化完成");
    
    // 初始化Socket连接（如果需要）
    var socketService = app.Services.GetRequiredService<ISocketService>();
    // 可以在配置中指定Socket服务器地址
    var socketHost = builder.Configuration.GetValue<string>("SocketService:Host") ?? "192.168.31.35";
    var socketPort = builder.Configuration.GetValue<int>("SocketService:Port", 5007);
    
    _ = Task.Run(async () =>
    {
        try
        {
            await socketService.ConnectAsync(socketHost, socketPort);
            logger.LogInformation("Socket服务连接成功");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Socket服务连接失败，将在运行时重试");
        }
    });
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "服务启动时发生错误");
}

app.Run(); 