# BlazorApp_Web - Blazor前端应用

## 项目概述

BlazorApp_Web 是无人机集群管理系统的前端应用，基于 Blazor Server 构建，提供直观的用户界面用于无人机监控、任务管理、数据分析和系统管理。采用现代化的响应式设计，支持实时数据更新和交互式图表展示。

## 🏗️ 项目架构

### 技术栈
- **Blazor Server (.NET 9.0)** - 服务端渲染框架
- **Bootstrap 5** - UI组件库
- **SignalR** - 实时通信
- **LiveCharts** - 图表组件
- **JavaScript Interop** - JS交互
- **CSS Grid & Flexbox** - 响应式布局

### 架构模式
- **组件化架构** - 可复用的Blazor组件
- **服务层模式** - 业务逻辑分离
- **实时数据绑定** - 自动UI更新
- **响应式设计** - 多设备适配
- **模块化开发** - 功能模块独立

## 📁 项目结构

```
BlazorApp_Web/
├── BlazorApp_Web/                    # 服务端项目
│   ├── Components/                   # Blazor组件
│   │   ├── Layout/                   # 布局组件
│   │   │   ├── MainLayout.razor      # 主布局
│   │   │   ├── NavMenu.razor         # 导航菜单
│   │   │   └── MainLayout.razor.css  # 布局样式
│   │   ├── Pages/                    # 页面组件
│   │   │   ├── Home.razor            # 首页
│   │   │   ├── Drone_Map.razor       # 无人机地图
│   │   │   ├── Task_Manage.razor     # 任务管理
│   │   │   ├── HistoryDataAnalysis.razor # 历史数据分析
│   │   │   └── Error.razor           # 错误页面
│   │   ├── Subassembly/              # 子组件
│   │   │   ├── DroneDataAnalysis.razor      # 无人机数据分析
│   │   │   ├── TaskDataAnalysis.razor       # 任务数据分析
│   │   │   ├── TimeRangeAnalysis.razor      # 时间范围分析
│   │   │   ├── StatisticsAnalysisTab.razor  # 统计分析
│   │   │   └── DataManagementTab.razor      # 数据管理
│   │   ├── App.razor                 # 应用根组件
│   │   ├── Routes.razor              # 路由配置
│   │   └── _Imports.razor            # 全局引用
│   ├── Service/                      # 服务层
│   │   └── HistoryApiService.cs      # 历史数据API服务
│   ├── wwwroot/                      # 静态资源
│   │   ├── css/                      # 样式文件
│   │   ├── js/                       # JavaScript文件
│   │   └── images/                   # 图片资源
│   ├── Program.cs                    # 程序入口
│   └── appsettings.json             # 应用配置
└── BlazorApp_Web.Client/            # 客户端项目
    ├── Pages/                       # 客户端页面
    ├── Program.cs                   # 客户端入口
    └── wwwroot/                     # 客户端静态资源
```

## 🚀 核心功能

### 1. 无人机监控界面

#### 实时地图显示 (Drone_Map.razor)
- **无人机位置** - 实时GPS坐标显示
- **状态指示** - 颜色编码状态显示
- **飞行轨迹** - 历史轨迹回放
- **区域管理** - 飞行区域划分

```razor
@page "/drone-map"
@using ClassLibrary_Core.Drone
@inject IJSRuntime JSRuntime

<div class="container-fluid">
    <div class="row">
        <div class="col-md-8">
            <!-- 地图显示区域 -->
            <div id="droneMap" style="height: 600px;"></div>
        </div>
        <div class="col-md-4">
            <!-- 无人机列表 -->
            <DroneListComponent Drones="@drones" OnDroneSelected="@SelectDrone" />
        </div>
    </div>
</div>
```

### 2. 任务管理界面 (Task_Manage.razor)

#### 任务创建和管理
- **视频任务创建** - 支持视频文件上传
- **任务状态监控** - 实时任务进度跟踪
- **子任务管理** - 详细的子任务操作
- **结果查看** - 处理结果图片展示

```razor
@page "/task-manage"
@using ClassLibrary_Core.Mission
@inject IHttpClientFactory HttpClientFactory

<!-- 任务创建模态框 -->
<div class="modal fade" id="videoTaskModal">
    <div class="modal-dialog modal-lg">
        <div class="modal-content">
            <!-- 任务创建表单 -->
            <InputFile OnChange="OnVideoFileSelected" accept=".mp4,.avi,.mov,.mkv" />
        </div>
    </div>
</div>
```

#### 集群控制面板
- **节点管理** - 启动/停止集群节点
- **状态监控** - 集群健康状态显示
- **资源监控** - CPU、内存使用情况

### 3. 历史数据分析 (HistoryDataAnalysis.razor)

#### 多维度数据分析
- **无人机数据分析** - 按时间范围查询数据
- **任务数据分析** - 任务执行情况统计
- **时间范围分析** - 自定义时间段分析
- **统计分析** - 性能指标和趋势分析

```razor
@page "/history-analysis"
@implements IDisposable
@inject IHttpClientFactory HttpClientFactory

<TabNavigation ActiveTab="@activeTab" OnTabChanged="@SetActiveTab">
    @if (activeTab == "drone")
    {
        <DroneDataAnalysis 
            OnDataLoaded="@LoadDroneData"
            AvailableDrones="@DataDrones"
            DataPoints="@DataPoints" />
    }
    <!-- 其他选项卡 -->
</TabNavigation>
```

#### 自动刷新机制
```csharp
private Timer? refreshTimer;

protected override async Task OnInitializedAsync()
{
    await LoadInitialData();
    
    // 启动定时器，每30秒刷新一次数据
    refreshTimer = new Timer(async _ => await RefreshData(), 
        null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
}
```

### 4. 系统概览组件

#### 实时统计卡片
- **无人机状态** - 在线/离线统计
- **任务统计** - 完成/进行中/失败任务数
- **性能指标** - 系统性能实时监控
- **告警信息** - 系统异常提醒

## 🎨 UI/UX设计

### 响应式布局
```css
/* 主布局样式 */
.main-layout {
    display: grid;
    grid-template-areas: 
        "header header"
        "nav content"
        "footer footer";
    grid-template-rows: auto 1fr auto;
    grid-template-columns: 250px 1fr;
    min-height: 100vh;
}

/* 移动端适配 */
@media (max-width: 768px) {
    .main-layout {
        grid-template-areas: 
            "header"
            "nav"
            "content"
            "footer";
        grid-template-columns: 1fr;
    }
}
```

### 主题配置
```css
:root {
    --primary-color: #0066cc;
    --secondary-color: #6c757d;
    --success-color: #28a745;
    --danger-color: #dc3545;
    --warning-color: #ffc107;
    --info-color: #17a2b8;
}
```

### 组件样式
- **卡片组件** - 统一的卡片样式
- **按钮组件** - 一致的交互反馈
- **表格组件** - 数据展示优化
- **图表组件** - 实时数据可视化

## 📊 数据可视化

### LiveCharts集成
```razor
@using LiveChartsCore
@using LiveChartsCore.SkiaSharpView.Blazor

<CartesianChart 
    Series="@Series"
    XAxes="@XAxes"
    YAxes="@YAxes"
    Title="@ChartTitle">
</CartesianChart>
```

### 图表类型
- **折线图** - 时间序列数据展示
- **柱状图** - 统计数据比较
- **饼图** - 比例数据展示
- **散点图** - 相关性分析

## 🔄 实时通信

### SignalR集成
```csharp
// 连接到无人机Hub
hubConnection = new HubConnectionBuilder()
    .WithUrl("/dronehub")
    .Build();

// 监听无人机状态更新
hubConnection.On<Drone>("DroneStatusUpdated", (drone) =>
{
    InvokeAsync(() =>
    {
        UpdateDroneStatus(drone);
        StateHasChanged();
    });
});
```

### 实时功能
- **无人机状态更新** - 实时位置和状态同步
- **任务进度更新** - 任务执行进度推送
- **系统告警** - 异常情况即时通知
- **性能指标** - 系统性能实时监控

## ⚙️ 配置管理

### 应用程序配置 (appsettings.json)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "ApiService": "http://localhost:5000"
  },
  "ApiEndpoints": {
    "DroneService": "/api/drones",
    "TaskService": "/api/tasks",
    "HistoryService": "/api/historydata"
  },
  "UI": {
    "RefreshInterval": 5000,
    "ChartUpdateInterval": 2000,
    "MaxDataPoints": 100
  }
}
```

### 服务注册
```csharp
// 添加HTTP客户端
builder.Services.AddHttpClient("ApiService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetConnectionString("ApiService"));
    client.Timeout = TimeSpan.FromSeconds(30);
});

// 添加业务服务
builder.Services.AddScoped<HistoryApiService>();
builder.Services.AddScoped<DroneService>();
builder.Services.AddScoped<TaskService>();
```

## 🔧 组件开发

### 自定义组件示例
```razor
@* DroneStatusCard.razor *@
<div class="card drone-status-card">
    <div class="card-header">
        <h5 class="card-title">
            <i class="bi bi-drone"></i> @Drone.Name
        </h5>
        <span class="badge @GetStatusBadgeClass()">@Drone.Status</span>
    </div>
    <div class="card-body">
        <p><strong>电量:</strong> @Drone.BatteryLevel%</p>
        <p><strong>位置:</strong> @Drone.Position.Latitude, @Drone.Position.Longitude</p>
        <p><strong>最后更新:</strong> @Drone.LastUpdate.ToString("HH:mm:ss")</p>
    </div>
</div>

@code {
    [Parameter] public Drone Drone { get; set; } = null!;
    [Parameter] public EventCallback<Drone> OnDroneClick { get; set; }

    private string GetStatusBadgeClass() => Drone.Status switch
    {
        DroneStatus.Idle => "bg-success",
        DroneStatus.InMission => "bg-primary",
        DroneStatus.Offline => "bg-secondary",
        DroneStatus.Emergency => "bg-danger",
        _ => "bg-warning"
    };
}
```

### 组件通信
```csharp
// 父子组件通信
[Parameter] public EventCallback<string> OnTabChanged { get; set; }

// 组件间状态共享
[Inject] public StateContainer StateContainer { get; set; }

// 服务注入
[Inject] public IHttpClientFactory HttpClientFactory { get; set; }
```

## 🚀 部署配置

### 项目文件配置
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\BlazorApp_Web.Client\BlazorApp_Web.Client.csproj" />
    <ProjectReference Include="..\..\ClassLibrary_Core\ClassLibrary_Core.csproj" />
    
    <PackageReference Include="LiveChartsCore.SkiaSharpView.Blazor" Version="2.0.0-rc5.4" />
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="9.0.6" />
  </ItemGroup>
</Project>
```

### Docker部署
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["BlazorApp_Web/BlazorApp_Web/BlazorApp_Web.csproj", "BlazorApp_Web/BlazorApp_Web/"]
COPY ["BlazorApp_Web/BlazorApp_Web.Client/BlazorApp_Web.Client.csproj", "BlazorApp_Web/BlazorApp_Web.Client/"]
RUN dotnet restore "BlazorApp_Web/BlazorApp_Web/BlazorApp_Web.csproj"
COPY . .
WORKDIR "/src/BlazorApp_Web/BlazorApp_Web"
RUN dotnet build "BlazorApp_Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BlazorApp_Web.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BlazorApp_Web.dll"]
```

## 📱 性能优化

### 渲染优化
```csharp
// 使用 ShouldRender 优化渲染
protected override bool ShouldRender()
{
    return hasDataChanged;
}

// 虚拟化长列表
<Virtualize Items="@largeDataSet" Context="item">
    <ItemTemplate>
        <div>@item.Name</div>
    </ItemTemplate>
</Virtualize>
```

### 内存管理
```csharp
// 实现 IDisposable
public void Dispose()
{
    hubConnection?.DisposeAsync();
    refreshTimer?.Dispose();
}

// 取消令牌使用
private readonly CancellationTokenSource _cancellationTokenSource = new();
```

## 🐛 故障排除

### 常见问题

1. **SignalR连接失败**
   ```csharp
   // 检查连接状态
   if (hubConnection.State == HubConnectionState.Disconnected)
   {
       await hubConnection.StartAsync();
   }
   ```

2. **API调用超时**
   ```csharp
   // 设置超时时间
   httpClient.Timeout = TimeSpan.FromSeconds(30);
   ```

3. **组件状态不更新**
   ```csharp
   // 手动触发状态更新
   await InvokeAsync(StateHasChanged);
   ```

### 调试技巧
```csharp
// 启用详细日志
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// 浏览器控制台输出
await JSRuntime.InvokeVoidAsync("console.log", "Debug message");
```

## 📚 开发指南

### 添加新页面
1. 在 `Pages/` 目录创建新的 `.razor` 文件
2. 添加 `@page` 指令定义路由
3. 在导航菜单中添加链接
4. 实现页面逻辑和UI

### 创建可复用组件
1. 在 `Components/` 目录创建组件
2. 定义 `[Parameter]` 属性
3. 实现组件逻辑
4. 添加样式文件

### 集成新的API
1. 在 `Service/` 目录创建服务类
2. 注册依赖注入
3. 在组件中注入和使用
4. 处理错误和异常

---

**维护者**: AspireApp 开发团队  
**更新时间**: 2024年12月 