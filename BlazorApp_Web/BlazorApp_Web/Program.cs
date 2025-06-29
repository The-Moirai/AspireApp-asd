using AspireApp_Drone.BlazorApp_Drone.Hubs;
using BlazorApp_Web.Client.Pages;
using BlazorApp_Web.Components;
using BlazorApp_Web.Service;

var builder = WebApplication.CreateBuilder(args);

// 添加 Aspire 默认服务（包含服务发现和弹性处理）
builder.AddServiceDefaults();

// 添加SignalR服务
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// 添加控制器服务
builder.Services.AddControllers();

// 添加Razor组件服务
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// 配置API服务的HttpClient（使用Aspire服务发现和弹性处理）
builder.Services.AddHttpClient("ApiService", client =>
{
    // 在开发环境中，如果Aspire服务发现不可用，使用localhost fallback
    if (builder.Environment.IsDevelopment())
    {
        // 优先使用Aspire服务发现，如果不可用则fallback到localhost
        client.BaseAddress = new Uri("https://apisercie-drone/"); // API服务的开发环境地址
    }
    else
    {
        // 生产环境使用Aspire服务发现
        client.BaseAddress = new Uri("https://apisercie-drone/");
    }
    
    client.Timeout = TimeSpan.FromSeconds(30);
    
    // 优化图片请求的缓存策略
    client.DefaultRequestHeaders.Add("Accept", "application/json,image/*");
    client.DefaultRequestHeaders.Add("Cache-Control", "no-cache"); // 确保获取最新图片
})
.AddServiceDiscovery() // 启用Aspire服务发现
.AddStandardResilienceHandler(); // 添加标准弹性处理（重试、熔断、超时）

// 添加历史数据服务的HttpClient
builder.Services.AddHttpClient("HistoryApi", client =>
{
    client.BaseAddress = new Uri("https://apisercie-drone/");
    client.Timeout = TimeSpan.FromSeconds(60); // 历史数据查询可能需要更长时间
})
.AddServiceDiscovery()
.AddStandardResilienceHandler();

// 注册数据服务（改为Scoped以支持并发请求）
builder.Services.AddScoped<HistoryApiService>();
builder.Services.AddHttpContextAccessor();
// 注册图片代理服务
builder.Services.AddHttpClient<ImageProxyService>(client =>
{
    // 图片代理服务使用本地HttpClient，无需配置BaseAddress
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "BlazorApp-ImageProxyService/1.0");
});

// 为代理控制器配置专门的HttpClient
builder.Services.AddHttpClient<BlazorApp_Web.Controllers.ImageProxyController>("ImageProxyClient", client =>
{
    if (builder.Environment.IsDevelopment())
    {
        client.BaseAddress = new Uri("https://apisercie-drone/");
    }
    else
    {
        client.BaseAddress = new Uri("https://apisercie-drone/");
    }
    
    client.Timeout = TimeSpan.FromSeconds(60); // 图片可能较大，设置更长超时
    
    // 配置图片请求的专门头信息
    client.DefaultRequestHeaders.Add("Accept", "image/*,application/json");
    client.DefaultRequestHeaders.Add("User-Agent", "BlazorApp-ImageProxy/1.0");
})
.AddServiceDiscovery()
.AddStandardResilienceHandler();

// 注册后台推送服务
builder.Services.AddHostedService<DronePushBackgroundService>();
builder.Services.AddHostedService<TaskPushBackgroundService>();

var app = builder.Build();

// 映射Aspire默认端点（健康检查、指标等）
app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// 配置静态文件服务 - 添加图片缓存策略
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // 为图片文件设置适当的缓存策略
        var fileExtension = Path.GetExtension(ctx.File.Name).ToLowerInvariant();
        if (fileExtension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".svg")
        {
            // 设置较短的缓存时间，支持ETag验证
            ctx.Context.Response.Headers.Add("Cache-Control", "public, max-age=3600, must-revalidate");
            ctx.Context.Response.Headers.Add("ETag", $"\"{ctx.File.Name}-{ctx.File.LastModified.Ticks}\"");
        }
    }
});

app.UseAntiforgery();
app.MapStaticAssets();  // 映射WebAssembly静态资源

// 配置SignalR Hub
app.MapHub<DroneHub>("/droneHub");
app.MapHub<TaskHub>("/taskshub");

// 映射控制器路由
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Counter).Assembly);

app.Run();