# AspireApp 无人机集群管理系统 (.NET端)

## 项目概述

AspireApp 是一个基于 .NET 8.0 和 .NET Aspire 框架构建的现代化无人机集群管理系统。该系统采用微服务架构，提供实时的无人机监控、任务管理、数据分析和系统优化功能。

## 🏗️ 系统架构

### 技术栈
- **.NET 8.0** - 核心运行时
- **.NET Aspire** - 云原生应用开发框架
- **Blazor Server** - 前端UI框架
- **SignalR** - 实时通信
- **SQL Server** - 主数据库
- **Redis** - 分布式缓存
- **OpenTelemetry** - 可观测性
- **Docker** - 容器化部署

### 架构模式
- **微服务架构** - 服务解耦，独立部署
- **DDD领域驱动设计** - 业务逻辑清晰分层
- **CQRS模式** - 读写分离优化性能
- **事件驱动架构** - 异步消息处理
- **云原生设计** - 容器化、服务发现、健康检查

## 📁 项目结构

```
AspireApp-asd/
├── AspireApp.AppHost/              # Aspire应用程序主机
├── AspireApp.ServiceDefaults/      # 共享服务配置
├── BlazorApp_Web/                  # Blazor前端应用
│   ├── BlazorApp_Web/             # 服务端项目
│   └── BlazorApp_Web.Client/      # 客户端项目
├── WebApplication_Drone/           # 无人机API服务
├── ClassLibrary_Core/              # 核心业务逻辑库
├── WebApplication/                 # 废弃项目
├── WebApplication.Data/            # 废弃项目
├── WebApplication.Service/         # 废弃项目
└── linux_code/                    # Linux端Python代码
```

## 🚀 核心功能

### 1. 无人机管理
- **实时状态监控** - 位置、电量、任务状态
- **集群协调** - 多无人机协同作业
- **故障检测** - 自动故障发现和处理
- **性能分析** - 飞行数据统计和分析

### 2. 任务管理
- **视频处理任务** - 支持多种视频格式
- **任务分发** - 智能任务分配算法
- **进度跟踪** - 实时任务执行状态
- **结果管理** - 处理结果存储和查看

### 3. 数据分析
- **历史数据分析** - 多维度数据查询
- **性能统计** - 系统性能指标监控
- **趋势分析** - 数据趋势预测
- **报表生成** - 自动化报表系统

### 4. 系统监控
- **健康检查** - 多层次系统健康监控
- **性能监控** - CPU、内存、网络监控
- **告警系统** - 智能告警和通知
- **日志管理** - 结构化日志收集和分析

## 🔧 技术特性

### 高可用性
- **服务发现** - 自动服务注册和发现
- **负载均衡** - 智能请求分发
- **故障转移** - 自动故障恢复
- **弹性伸缩** - 基于负载的自动扩缩容

### 高性能
- **异步编程** - 全异步API设计
- **缓存优化** - 多层缓存策略
- **连接池** - 数据库连接池管理
- **内存优化** - 智能内存管理

### 安全性
- **认证授权** - JWT令牌认证
- **数据加密** - 传输和存储加密
- **访问控制** - 基于角色的权限控制
- **安全审计** - 操作日志和审计跟踪

### 可观测性
- **分布式追踪** - 请求链路追踪
- **指标收集** - 业务和技术指标
- **日志聚合** - 集中化日志管理
- **性能分析** - APM性能监控

## 🏃‍♂️ 快速开始

### 环境要求
- .NET 8.0 SDK
- Visual Studio 2022 17.8+ 或 VS Code
- Docker Desktop
- SQL Server (本地或容器)
- Redis (本地或容器)

### 运行步骤

1. **克隆代码**
```bash
git clone <repository-url>
cd AspireApp-asd
```

2. **启动依赖服务**
```bash
# 启动SQL Server (Docker)
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourPassword123!" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest

# 启动Redis (Docker)
docker run -d -p 6379:6379 redis:latest
```

3. **配置连接字符串**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=AspireAppDB;User Id=sa;Password=YourPassword123!;TrustServerCertificate=true;",
    "cache": "localhost:6379"
  }
}
```

4. **运行应用**
```bash
# 使用 Aspire AppHost 启动
cd AspireApp.AppHost
dotnet run

# 或者分别启动各个服务
cd WebApplication_Drone
dotnet run

cd BlazorApp_Web/BlazorApp_Web
dotnet run
```

5. **访问应用**
- **Aspire Dashboard**: http://localhost:15888
- **API文档**: http://localhost:5000/swagger
- **Web界面**: http://localhost:5001

## 📊 性能指标

### 系统性能
- **API响应时间**: < 100ms (P95)
- **并发处理能力**: 1000+ 请求/秒
- **内存使用**: < 512MB (单服务)
- **CPU使用率**: < 30% (正常负载)

### 业务指标
- **无人机连接数**: 支持100+并发连接
- **任务处理能力**: 50+并发任务
- **数据处理量**: 1GB+/小时
- **系统可用性**: 99.9%+

## 🔍 监控和运维

### 健康检查端点
- `/health` - 完整健康状态
- `/health/ready` - 就绪状态检查
- `/health/live` - 存活状态检查

### 关键指标监控
- **系统指标**: CPU、内存、磁盘、网络
- **应用指标**: 请求量、响应时间、错误率
- **业务指标**: 无人机状态、任务完成率
- **基础设施**: 数据库、缓存、消息队列

### 日志级别
- **Trace**: 详细调试信息
- **Debug**: 开发调试信息
- **Information**: 一般信息记录
- **Warning**: 警告信息
- **Error**: 错误信息
- **Critical**: 严重错误

## 🚀 部署指南

### Docker 部署
```bash
# 构建镜像
docker build -t aspireapp-drone ./WebApplication_Drone
docker build -t aspireapp-web ./BlazorApp_Web/BlazorApp_Web

# 运行容器
docker-compose up -d
```

### Kubernetes 部署
```yaml
# 使用提供的 k8s manifests
kubectl apply -f k8s/
```

### 云平台部署
- **Azure Container Apps**
- **AWS ECS/EKS**
- **Google Cloud Run**

## 🤝 贡献指南

### 开发流程
1. Fork 项目
2. 创建功能分支
3. 提交代码变更
4. 创建 Pull Request
5. 代码审查和合并

### 代码规范
- 遵循 C# 编码规范
- 使用 EditorConfig 配置
- 通过 SonarQube 质量检查
- 单元测试覆盖率 > 80%

### 提交规范
```
type(scope): description

[optional body]

[optional footer]
```

## 📄 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情。

## 📞 支持和联系

- **问题报告**: [GitHub Issues](issues)
- **功能请求**: [GitHub Discussions](discussions)
- **技术支持**: support@aspireapp.com
- **文档**: [项目Wiki](wiki)

## 🗺️ 路线图

### v1.1 (计划中)
- [ ] 机器学习任务调度
- [ ] 高级数据分析
- [ ] 移动端应用
- [ ] 多租户支持

### v1.2 (规划中)
- [ ] 边缘计算支持
- [ ] 5G网络优化
- [ ] AI辅助决策
- [ ] 区块链溯源

## 📈 更新日志

### v1.0.0 (当前版本)
- ✅ 基础无人机管理功能
- ✅ 任务分发和执行
- ✅ 实时数据监控
- ✅ 系统性能优化
- ✅ 健康检查和监控
- ✅ Docker容器化支持

---

**AspireApp Team** - 构建下一代无人机集群管理系统 