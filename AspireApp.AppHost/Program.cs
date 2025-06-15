var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");
var sql = builder.AddSqlServer("sql")
                 .WithLifetime(ContainerLifetime.Persistent);

var databaseName = "app-db";
var creationScript = $$"""
    IF DB_ID('{{databaseName}}') IS NULL
        CREATE DATABASE [{{databaseName}}];
    GO

    -- Use the database
    USE [{{databaseName}}];
    GO

    CREATE TABLE Drones  (
          Id INT IDENTITY(1,1) PRIMARY KEY,          -- 无人机历史记录表主键
          DroneId NVARCHAR(50) NOT NULL,             -- 无人机唯一标识符
    	  ModelType NVARCHAR(50) NOT NULL,			 -- 无人机类型-实体/虚拟
          Latitude FLOAT NOT NULL,                   -- 纬度
          Longitude FLOAT NOT NULL,                  -- 经度
      );
    GO
    CREATE TABLE DroneStatusHistory (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        DroneId INT NOT NULL,
        Status TINYINT NOT NULL,
        Timestamp DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CpuUsedRate DECIMAL(5,2) NULL,							-- cpu使用率
        LeftBandwidth DECIMAL(10,2) NULL,						-- 带宽使用率
        Latitude DECIMAL(10,7) NULL,							-- 纬度
        Longitude DECIMAL(10,7) NULL,							-- 经度

        -- 外键和索引
        FOREIGN KEY (DroneId) REFERENCES Drones(Id),
        INDEX IX_DroneStatusHistory_Drone NONCLUSTERED (DroneId, Timestamp DESC)
    );
    CREATE TABLE MainTasks (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),			-- 任务id
        Description NVARCHAR(500) NOT NULL,							-- 任务描述
        Status TINYINT NOT NULL,									-- 对应TaskStatus枚举
        CreationTime DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),	-- 创建时间
        CompletedTime DATETIME2 NULL,								-- 完成时间

        -- 索引
        INDEX IX_MainTasks_Status NONCLUSTERED (Status),
        INDEX IX_MainTasks_CreationTime NONCLUSTERED (CreationTime DESC)
    );

    -- 状态枚举注释
    EXEC sp_addextendedproperty 
        @name = N'MS_Description', @value = '0:Pending, 1:InProgress, 2:Completed, 3:Cancelled, 4:Failed',
        @level0type = N'SCHEMA', @level0name = 'dbo',
        @level1type = N'TABLE', @level1name = 'MainTasks',
        @level2type = N'COLUMN', @level2name = 'Status';
    GO
    CREATE TABLE SubTasks (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),			--任务id
        Description NVARCHAR(500) NOT NULL,							--任务描述/任务名
        Status TINYINT NOT NULL,									--任务状态
        CreationTime DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),	--创建时间
        AssignedTime DATETIME2 NULL,								--分配时间
        CompletedTime DATETIME2 NULL,								--完成时间
        AssignedDrone NVARCHAR(100) NULL,							--任务执行无人机
        ParentTaskId UNIQUEIDENTIFIER NOT NULL,						--父任务id
    	ReassignmentCount INT NOT NULL DEFAULT 0,                   --重分配次数

        -- 外键约束
        CONSTRAINT FK_SubTasks_MainTasks FOREIGN KEY (ParentTaskId) 
            REFERENCES MainTasks(Id) ON DELETE CASCADE,

        -- 索引
        INDEX IX_SubTasks_ParentTaskId NONCLUSTERED (ParentTaskId),
        INDEX IX_SubTasks_Status NONCLUSTERED (Status),
        INDEX IX_SubTasks_AssignedDrone NONCLUSTERED (AssignedDrone),
        INDEX IX_SubTasks_Completion NONCLUSTERED (Status, CompletedTime)
    );

    -- 添加状态枚举注释
    EXEC sp_addextendedproperty 
        @name = N'MS_Description', @value = '0:Pending, 1:InProgress, 2:Completed, 3:Cancelled, 4:Failed',
        @level0type = N'SCHEMA', @level0name = 'dbo',
        @level1type = N'TABLE', @level1name = 'SubTasks',
        @level2type = N'COLUMN', @level2name = 'Status';
    GO
    CREATE TABLE DroneSubTasks (
        DroneId INT NOT NULL,											--无人机id
        SubTaskId UNIQUEIDENTIFIER NOT NULL,							--子任务id
        AssignmentTime DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),		--分配时间
        CompletionStatus TINYINT DEFAULT 0,								--分配时状态 0:Assigned, 1:InProgress, 2:Completed, 3:Failed

        -- 主键和约束
        PRIMARY KEY (DroneId, SubTaskId),
        FOREIGN KEY (DroneId) REFERENCES Drones(Id),
        FOREIGN KEY (SubTaskId) REFERENCES SubTasks(Id),

        -- 索引
        INDEX IX_DroneSubTasks_SubTask NONCLUSTERED (SubTaskId),
        INDEX IX_DroneSubTasks_Status NONCLUSTERED (CompletionStatus)
    );

    """;
var db = sql.AddDatabase(databaseName)
            .WithCreationScript(creationScript);
var apiService=builder.AddProject<Projects.WebApplication_Drone>("apisercie-drone")
    .WithExternalHttpEndpoints()
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(db)
    .WaitFor(db);

builder.AddProject<Projects.BlazorApp_Web>("blazorapp-web")
    .WithExternalHttpEndpoints().WithReference(apiService)
    .WaitFor(apiService)
    .WithReference(cache)
    .WaitFor(cache);

builder.Build().Run();
