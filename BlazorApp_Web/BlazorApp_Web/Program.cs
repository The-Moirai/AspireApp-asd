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
///���������ݲ�������
///</summary>
builder.Services.AddHttpClient("ApiService", client =>
{
    client.BaseAddress = new Uri("https://apisercie-drone/"); // Aspire ��������ʵ��API��ַ
});

//���ݶ�ʱ���ͷ���
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
app.MapStaticAssets();  // ӳ��WebAssembly��̬��Դ
app.MapHub<DroneHub>("/droneHub");
app.MapHub<TaskHub>("/taskshub");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Counter).Assembly);

app.Run();
