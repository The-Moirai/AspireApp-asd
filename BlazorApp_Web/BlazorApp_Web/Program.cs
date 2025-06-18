using AspireApp_Drone.BlazorApp_Drone.Hubs;
using BlazorApp_Web.Client.Pages;
using BlazorApp_Web.Components;
using BlazorApp_Web.Service;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();


builder.Services.AddSignalR();
// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

///<summary>
///锟斤拷锟斤拷锟斤拷锟斤拷锟捷诧拷锟斤拷锟斤拷锟斤拷
///</summary>
builder.Services.AddHttpClient("ApiService", client =>
{
    client.BaseAddress = new Uri("https://apisercie-drone/"); // Aspire 锟斤拷锟斤拷锟斤拷锟斤拷实锟斤拷API锟斤拷址
});


//锟斤拷锟捷讹拷时锟斤拷锟酵凤拷锟斤拷
// 添加数据服务
builder.Services.AddScoped<HistoryApiService>();
//数据定时推送服务

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