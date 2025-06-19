using Microsoft.Data.SqlClient;
using Microsoft.OpenApi.Models;
using WebApplication_Drone;
using WebApplication_Drone.Services;
using ClassLibrary_Core.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// 配置路由选项
builder.Services.Configure<RouteOptions>(options =>
{
    options.ConstraintMap.Add("string", typeof(string));
});

// 添加Aspire默认服务
builder.AddServiceDefaults();

// 添加业务服务配置
builder.AddBusinessServices();

// 添加分布式缓存
builder.AddDistributedCaching();

// 添加Redis连接
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("cache");
    return ConnectionMultiplexer.Connect(connectionString!);
});

// 添加控制器服务
builder.Services.AddControllers();

// 配置Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "AspireApp Drone API", 
        Version = "v1",
        Description = "无人机集群管理系统API",
        Contact = new OpenApiContact
        {
            Name = "AspireApp Team"
        }
    });

    // 添加文件上传支持
    c.OperationFilter<SwaggerFileOperationFilter>();

    // 配置IFormFile类型参数
    c.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });

    // 添加JWT认证支持（如果需要）
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT授权标头使用Bearer方案。示例：\"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
});

// 注册数据访问服务（改为Scoped）
builder.Services.AddScoped<IDatabaseService, SqlserverService>();

// 注册业务逻辑服务（改为Scoped）
builder.Services.AddScoped<IDroneDataService, DroneDataService>();
builder.Services.AddScoped<ITaskDataService, TaskDataService>();

// 注册Socket服务（保持Singleton，因为需要维持连接状态）
builder.Services.AddSingleton<SocketService>();
builder.Services.AddSingleton<MissionSocketService>();

// 注册后台服务
builder.Services.AddHostedService<SocketBackgroundService>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "AspireApp Drone API v1");
        c.RoutePrefix = string.Empty; // 设置Swagger UI为根路径
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
