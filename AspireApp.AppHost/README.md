# AspireApp.AppHost - Aspire应用程序主机

## 项目概述

AspireApp.AppHost 是基于 .NET Aspire 框架的应用程序主机项目，负责整个分布式系统的编排、配置和管理。它定义了系统的拓扑结构、服务依赖关系和基础设施配置。

## 🏗️ 项目架构

### 技术栈
- **.NET 8.0**
- **.NET Aspire 9.0**
- **SQL Server** (通过 Aspire.Hosting.SqlServer)
- **Redis** (通过 Aspire.Hosting.Redis)

### 核心职责
- **服务编排** - 定义服务启动顺序和依赖关系
- **配置管理** - 集中管理所有服务的配置
- **基础设施管理** - 管理数据库、缓存等基础设施
- **健康检查** - 监控所有服务的健康状态
- **服务发现** - 提供服务注册和发现功能

## 📁 项目结构

```
AspireApp.AppHost/
├── Program.cs                    # 主程序入口
├── AspireApp.AppHost.csproj     # 项目文件
├── Properties/
│   └── launchSettings.json     # 启动配置
├── appsettings.json             # 应用配置
├── appsettings.Development.json # 开发环境配置
└── bin/                         # 编译输出
```

## 🚀 核心功能

### 1. 服务编排配置

```csharp
// 添加SQL Server数据库
var db = sql.AddDatabase(databaseName)
    .WithCreationScript(creationScript);

// 添加Redis缓存
var cache = builder.AddRedis("cache");

// 添加API服务
var apiService = builder.AddProject<Projects.WebApplication_Drone>("apisercie-drone")
    .WithExternalHttpEndpoints()
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(db)
    .WaitFor(db);

// 添加Web应用
builder.AddProject<Projects.BlazorApp_Web>("blazorapp-web")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithReference(cache)
    .WaitFor(cache);
```

### 2. 数据库初始化

项目包含完整的数据库创建脚本，自动创建以下表结构：

#### 核心表
- **Drones** - 无人机基本信息
- **MainTasks** - 主任务表
- **SubTasks** - 子任务表
- **DroneDataPoints** - 无人机数据点
- **TaskAssignments** - 任务分配记录

#### 历史和审计表
- **SubTaskHistory** - 子任务状态变更历史
- **DroneStatusHistory** - 无人机状态变更历史
- **TaskAssignmentHistory** - 任务分配历史

#### 触发器
- **自动状态跟踪** - 状态变更时自动记录历史
- **数据完整性** - 确保数据一致性
- **审计日志** - 记录所有重要操作

### 3. 服务依赖管理

```csharp
// 定义服务依赖关系
apiService
    .WithReference(cache)      // API服务依赖缓存
    .WaitFor(cache)           // 等待缓存服务就绪
    .WithReference(db)        // API服务依赖数据库
    .WaitFor(db);            // 等待数据库就绪

blazorApp
    .WithReference(apiService) // Web应用依赖API服务
    .WaitFor(apiService);     // 等待API服务就绪
```

## ⚙️ 配置管理

### 应用程序配置 (appsettings.json)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "AllowedHosts": "*"
}
```

### 开发环境配置 (appsettings.Development.json)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### 启动配置 (launchSettings.json)
```json
{
  "profiles": {
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:15888;http://localhost:15889",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

## 🔧 项目配置

### 项目文件 (AspireApp.AppHost.csproj)
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="Aspire.AppHost.Sdk" Version="9.0.0" />

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsAspireHost>true</IsAspireHost>
    <UserSecretsId>724772dd-7640-4981-ab2f-6ce79fd4772a</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.AppHost" Version="9.3.1" />
    <PackageReference Include="Aspire.Hosting.Redis" Version="9.3.1" />
    <PackageReference Include="Aspire.Hosting.SqlServer" Version="9.3.1" />
    <PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks" Version="9.0.6" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\BlazorApp_Web\BlazorApp_Web\BlazorApp_Web.csproj" />
    <ProjectReference Include="..\WebApplication_Drone\WebApplication_Drone.csproj" />
  </ItemGroup>
</Project>
```

## 🚀 运行和部署

### 本地开发运行
```bash
# 进入项目目录
cd AspireApp.AppHost

# 运行应用主机
dotnet run
```

### 访问地址
- **Aspire Dashboard**: https://localhost:15888
- **应用监控**: http://localhost:15889

### Docker 部署
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["AspireApp.AppHost/AspireApp.AppHost.csproj", "AspireApp.AppHost/"]
RUN dotnet restore "AspireApp.AppHost/AspireApp.AppHost.csproj"
COPY . .
WORKDIR "/src/AspireApp.AppHost"
RUN dotnet build "AspireApp.AppHost.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AspireApp.AppHost.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AspireApp.AppHost.dll"]
```

## 📊 监控和观测

### Aspire Dashboard 功能
- **服务拓扑图** - 可视化服务依赖关系
- **实时监控** - 服务状态、性能指标
- **日志聚合** - 集中查看所有服务日志
- **分布式追踪** - 请求链路追踪
- **配置管理** - 运行时配置查看和修改

### 健康检查
```csharp
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())
    .AddCheck("database", () => /* 数据库健康检查 */)
    .AddCheck("cache", () => /* 缓存健康检查 */);
```

### 遥测数据
- **指标收集** - 自动收集性能指标
- **日志记录** - 结构化日志输出
- **追踪数据** - OpenTelemetry 兼容的追踪数据

## 🔧 开发指南

### 添加新服务
```csharp
// 在 Program.cs 中添加新服务
var newService = builder.AddProject<Projects.NewService>("new-service")
    .WithExternalHttpEndpoints()
    .WithReference(db)
    .WaitFor(db);
```

### 添加新的基础设施
```csharp
// 添加新的外部依赖
var messageQueue = builder.AddRabbitMQ("messaging");
var monitoring = builder.AddPrometheus("monitoring");
```

### 环境变量配置
```csharp
// 根据环境配置不同的行为
if (builder.Environment.IsDevelopment())
{
    // 开发环境特定配置
}
else
{
    // 生产环境特定配置
}
```

## 🐛 故障排除

### 常见问题

1. **服务启动失败**
   - 检查端口占用情况
   - 验证依赖服务是否正常
   - 查看 Aspire Dashboard 错误信息

2. **数据库连接问题**
   - 确认 SQL Server 服务运行状态
   - 检查连接字符串配置
   - 验证数据库权限设置

3. **缓存连接问题**
   - 确认 Redis 服务运行状态
   - 检查网络连接
   - 验证 Redis 配置

### 调试技巧
```csharp
// 启用详细日志
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// 添加调试输出
Console.WriteLine($"Service {serviceName} starting...");
```

## 📚 相关文档

- [.NET Aspire 官方文档](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [服务编排最佳实践](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/app-host-overview)
- [配置管理指南](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/configuration)
- [监控和遥测](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/telemetry)

---

**维护者**: AspireApp 开发团队  
**更新时间**: 2024年12月 