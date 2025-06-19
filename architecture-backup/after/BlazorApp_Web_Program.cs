using AspireApp_Drone.BlazorApp_Drone.Hubs;
using BlazorApp_Web.Client.Pages;
using BlazorApp_Web.Components;
using BlazorApp_Web.Service;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 添加Aspire默认服务
builder.AddServiceDefaults();

// 添加业务服务配置
builder.AddBusinessServices();

// 添加分布式缓存
builder.AddDistributedCaching();

// 添加SignalR服务
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// 添加Razor组件服务
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// 配置HttpClient（使用弹性策略）
builder.Services.AddHttpClient("ApiService", client =>
{
    client.BaseAddress = new Uri("https://apisercie-drone/"); // Aspire服务发现
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddStandardResilienceHandler(); // 添加标准弹性处理

// 添加具名HttpClient
builder.Services.AddHttpClient("HistoryApi", client =>
{
    client.BaseAddress = new Uri("https://apisercie-drone/");
    client.Timeout = TimeSpan.FromSeconds(60); // 历史数据查询可能需要更长时间
})
.AddStandardResilienceHandler();

// 注册数据服务（改为Scoped）
builder.Services.AddScoped<HistoryApiService>();

// 注册后台推送服务
builder.Services.AddHostedService<DronePushBackgroundService>();
builder.Services.AddHostedService<TaskPushBackgroundService>();
var app = builder.Build();

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

app.UseStaticFiles();
app.UseAntiforgery();
app.MapStaticAssets();  // 映锟斤拷WebAssembly锟斤拷态锟斤拷源
app.MapHub<DroneHub>("/droneHub");
app.MapHub<TaskHub>("/taskshub");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Counter).Assembly);

app.Run();