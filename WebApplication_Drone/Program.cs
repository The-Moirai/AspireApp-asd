using Microsoft.Data.SqlClient;
using Microsoft.OpenApi.Models;
using WebApplication_Drone;
using WebApplication_Drone.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<RouteOptions>(options =>
{
    options.ConstraintMap.Add("string", typeof(string));
});

builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Drone API", Version = "v1" });

    // �����ļ��ϴ�֧��
    c.OperationFilter<SwaggerFileOperationFilter>();

    // ����IFormFile���Ͳ���
    c.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });
});
builder.Services.AddSingleton<SqlserverService>();
//Api����ʱ�轫socket����socket���ӷ������ĺ�̨����ע�͵�
//socket���ӷ�����
builder.Services.AddSingleton<SocketService>();
builder.Services.AddSingleton<MissionSocketService>();
//socket���ӷ������ĺ�̨����
builder.Services.AddHostedService<SocketBackgroundService>();
// �������ݷ���
builder.Services.AddSingleton<DroneDataService>();
builder.Services.AddSingleton<TaskDataService>();
var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();


app.MapControllers();

app.Run();
