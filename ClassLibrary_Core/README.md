# ClassLibrary_Core - 核心业务逻辑库

## 项目概述

ClassLibrary_Core 是无人机集群管理系统的核心业务逻辑库，包含了系统中所有的数据模型、业务实体、通用组件和共享接口。该库被所有其他项目引用，提供了统一的数据结构和业务规则定义。

## 🏗️ 项目架构

### 技术栈
- **.NET 8.0** - 目标框架
- **C# 12** - 编程语言
- **System.ComponentModel.DataAnnotations** - 数据验证
- **System.Text.Json** - JSON序列化

### 设计原则
- **领域驱动设计 (DDD)** - 按业务领域组织代码
- **SOLID原则** - 单一职责、开闭原则等
- **数据传输对象 (DTO)** - 数据传输优化
- **值对象模式** - 不可变数据结构
- **聚合根模式** - 数据一致性保证

## 📁 项目结构

```
ClassLibrary_Core/
├── Common/                      # 通用组件
│   ├── GPSPosition.cs           # GPS位置信息
│   └── TaskUploadDto.cs         # 任务上传DTO
├── Data/                        # 数据模型
│   ├── DroneDataPoint.cs        # 无人机数据点
│   ├── DroneDataRequest.cs      # 无人机数据请求
│   ├── DroneStatusHistory.cs    # 无人机状态历史
│   ├── SystemOverview.cs        # 系统概览
│   ├── TaskStatistics.cs        # 任务统计
│   └── TaskPerformanceAnalysis.cs # 任务性能分析
├── Drone/                       # 无人机领域
│   ├── Drone.cs                 # 无人机实体
│   ├── DroneStatus.cs           # 无人机状态枚举
│   └── ModelStatus.cs           # 模型状态
├── Message/                     # 消息通信
│   ├── Message.cs               # 基础消息类
│   ├── Message_Send.cs          # 发送消息
│   └── MessageFromNode.cs       # 节点消息
├── Mission/                     # 任务领域
│   ├── MainTask.cs              # 主任务
│   ├── MissionHistory.cs        # 任务历史
│   ├── MissionStatus.cs         # 任务状态
│   └── SubTask.cs               # 子任务
└── ClassLibrary_Core.csproj     # 项目文件
```

## 🚀 核心领域模型

### 1. 无人机领域 (Drone)

#### 无人机实体 (Drone.cs)
- **基本属性**: ID、名称、状态、位置、电量等
- **业务方法**: 可用性检查、维护需求判断、位置更新
- **元数据支持**: 扩展属性存储

#### 无人机状态枚举 (DroneStatus.cs)
- **Offline** - 离线
- **Idle** - 空闲
- **InMission** - 执行任务中
- **Returning** - 返航中
- **Charging** - 充电中
- **Maintenance** - 维护中
- **Emergency** - 紧急状态

### 2. 任务领域 (Mission)

#### 主任务 (MainTask.cs)
- **生命周期管理**: 创建、启动、完成时间跟踪
- **子任务管理**: 子任务集合和进度计算
- **优先级支持**: 任务优先级排序
- **参数配置**: 灵活的任务参数设置

#### 子任务 (SubTask.cs)
- **任务分配**: 无人机分配和时间记录
- **结果管理**: 处理结果和图片URL存储
- **状态跟踪**: 详细的执行状态管理
- **图片处理**: 支持多种图片结果类型

### 3. 数据模型 (Data)

#### 无人机数据点 (DroneDataPoint.cs)
- **时序数据**: 时间戳、位置、状态记录
- **传感器数据**: 电量、速度、高度等指标
- **扩展传感器**: 自定义传感器数据支持
- **计算属性**: 低电量、高速等状态判断

#### 系统概览 (SystemOverview.cs)
- **统计信息**: 无人机和任务的统计数据
- **性能指标**: 可用率、成功率等关键指标
- **告警信息**: 系统异常和警告信息
- **实时更新**: 最后更新时间跟踪

### 4. 通用组件 (Common)

#### GPS位置 (GPSPosition.cs)
- **坐标信息**: 经纬度、海拔、时间戳
- **距离计算**: 两点间距离计算（哈弗辛公式）
- **区域判断**: 判断是否在指定区域内
- **格式化输出**: 友好的坐标显示格式

#### 任务上传DTO (TaskUploadDto.cs)
- **数据验证**: 内置验证规则和错误消息
- **参数支持**: 灵活的任务参数配置
- **标签系统**: 任务分类和检索支持
- **验证方法**: 自定义验证逻辑

## 🔧 业务规则引擎

### 任务分配规则
- **无人机可用性检查**
- **距离限制验证**
- **电量要求确认**
- **维护状态排除**

### 优先级计算
- **基础优先级**
- **紧急任务提升**
- **超时任务优先**
- **动态优先级调整**

### 系统健康检查
- **无人机可用率监控**
- **任务成功率跟踪**
- **系统正常运行时间**
- **自动告警生成**

## 📊 数据统计和分析

### 任务统计 (TaskStatistics.cs)
- **完成率计算**
- **平均执行时间**
- **状态分布统计**
- **优先级分析**
- **趋势数据生成**

### 性能分析 (TaskPerformanceAnalysis.cs)
- **响应时间分析**
- **吞吐量计算**
- **错误率统计**
- **瓶颈识别**
- **性能优化建议**

## 🔧 扩展方法和工具

### 集合扩展方法
- **状态过滤**: 按任务状态筛选
- **时间范围过滤**: 按时间段查询
- **可用无人机筛选**: 获取可用设备
- **范围查询**: 地理位置范围筛选

### 工具类
- **ID生成器**: 唯一标识符生成
- **时间格式化**: 友好的时间显示
- **文件大小格式化**: 可读的大小显示
- **数据转换工具**: 类型转换和验证

## 🔧 项目配置

### 项目文件特性
- **目标框架**: .NET 8.0
- **可空引用类型**: 启用
- **隐式using**: 简化引用
- **文档生成**: XML文档自动生成

### 依赖包
- **System.ComponentModel.Annotations**: 数据验证
- **System.Text.Json**: JSON序列化

## 📚 使用示例

### 创建和管理任务
```csharp
// 创建主任务
var mainTask = new MainTask
{
    Description = "视频处理任务",
    Priority = 5,
    Status = TaskStatus.Created
};

// 添加子任务
var subTask = new SubTask
{
    Description = "处理视频片段1"
};
mainTask.AddSubTask(subTask);

// 分配给无人机
subTask.AssignToDrone("DRONE_001");
```

### 数据统计分析
```csharp
// 计算任务统计
var statistics = new TaskStatistics()
    .CalculateFromTasks(allTasks);

// 性能分析
var analysis = new TaskPerformanceAnalysis();
analysis.AnalyzePerformance(tasks, drones);

// 系统健康检查
var alerts = BusinessRules.CheckSystemHealth(overview);
```

## 🔍 最佳实践

### 数据模型设计
- 使用值对象表示不可变数据
- 实现业务规则验证
- 提供清晰的API接口
- 使用强类型避免原始类型

### 性能优化
- 延迟加载大型集合
- 使用索引器优化查找
- 实现对象池减少GC压力
- 缓存计算结果

### 可维护性
- 遵循单一职责原则
- 使用依赖倒置
- 编写单元测试
- 提供详细的XML文档

---

**维护者**: AspireApp 开发团队  
**更新时间**: 2024年12月 