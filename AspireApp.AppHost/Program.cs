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

        -- 无人机表
    CREATE TABLE Drones (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        DroneId NVARCHAR(50) NOT NULL UNIQUE,  -- 物理无人机ID
        ModelType NVARCHAR(50) NOT NULL CHECK (ModelType IN ('实体', '虚拟')),
        RegistrationDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
         LastHeartbeat DATETIME2 NULL,
        INDEX IX_Drones_DroneId (DroneId)
    );
    GO

    -- 主任务表
    CREATE TABLE MainTasks (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        Description NVARCHAR(500) NOT NULL,
        Status TINYINT NOT NULL CHECK (Status BETWEEN 0 AND 4),  -- 0-4状态
        CreationTime DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CompletedTime DATETIME2 NULL,
        CreatedBy NVARCHAR(128) NOT NULL DEFAULT SUSER_SNAME(),

        INDEX IX_MainTasks_Status (Status),
        INDEX IX_MainTasks_CreationTime (CreationTime DESC),
        INDEX IX_MainTasks_Status_CreationTime (Status, CreationTime DESC)
    );
    GO

    -- 添加状态枚举注释
    EXEC sp_addextendedproperty 
        @name = N'MS_Description', @value = '0:Pending, 1:InProgress, 2:Completed, 3:Cancelled, 4:Failed',
        @level0type = N'SCHEMA', @level0name = 'dbo',
        @level1type = N'TABLE', @level1name = 'MainTasks',
        @level2type = N'COLUMN', @level2name = 'Status';
    GO

    -- 子任务表
    CREATE TABLE SubTasks (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        Description NVARCHAR(500) NOT NULL,
        Status TINYINT NOT NULL CHECK (Status BETWEEN 0 AND 7),  -- 0-4状态
        CreationTime DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        AssignedTime DATETIME2 NULL,
        CompletedTime DATETIME2 NULL,
        ParentTaskId UNIQUEIDENTIFIER NOT NULL,
        ReassignmentCount INT NOT NULL DEFAULT 0,

        -- 外键约束
        CONSTRAINT FK_SubTasks_MainTasks FOREIGN KEY (ParentTaskId) 
            REFERENCES MainTasks(Id) ON DELETE CASCADE,

        -- 索引
        INDEX IX_SubTasks_ParentTaskId (ParentTaskId),
        INDEX IX_SubTasks_Status_Completion (Status, CompletedTime),
        INDEX IX_SubTasks_CreationTime (CreationTime DESC),
        INDEX IX_SubTasks_ParentTask_CreationTime (ParentTaskId, CreationTime),
        INDEX IX_SubTasks_Status_ParentTask (Status, ParentTaskId)
    );
    GO

    -- 添加状态枚举注释
    EXEC sp_addextendedproperty 
        @name = N'MS_Description', @value = '0:Created, 1:WaitingForActivation, 2:WaitingToRun, 3:Running, 4:WaitingForChildrenToComplete, 5:RanToCompletion,6:Canceled,7:Faulted',
        @level0type = N'SCHEMA', @level0name = 'dbo',
        @level1type = N'TABLE', @level1name = 'SubTasks',
        @level2type = N'COLUMN', @level2name = 'Status';
    GO

    -- 无人机状态历史表
    CREATE TABLE DroneStatusHistory (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        DroneId UNIQUEIDENTIFIER NOT NULL,
        Status TINYINT NOT NULL,
        Timestamp DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CpuUsage DECIMAL(5,2) NULL,  -- CPU使用率%
        BandwidthAvailable DECIMAL(6,2) NULL,  -- 可用带宽 Mbps
        MemoryUsage DECIMAL(6,2) NULL,  -- 内存使用率%
        Latitude DECIMAL(10,7) NULL,
        Longitude DECIMAL(10,7) NULL,
        BatteryLevel DECIMAL(5,2) NULL,  -- 电池剩余%
        NetworkStrength TINYINT NULL,  -- 网络强度 1-5

        -- 外键约束
        FOREIGN KEY (DroneId) REFERENCES Drones(Id) ON DELETE CASCADE,

        -- 索引优化
        INDEX IX_DroneStatusHistory_DroneTime (DroneId, Timestamp DESC),
        INDEX IX_DroneStatusHistory_Location (Latitude, Longitude),
        INDEX IX_DroneStatusHistory_Timestamp (Timestamp DESC),
        INDEX IX_DroneStatusHistory_Status_Time (Status, Timestamp DESC),
        INDEX IX_DroneStatusHistory_TimeRange (Timestamp, DroneId)  -- 支持时间范围查询
    );
    GO

    -- 无人机-子任务关联表
    CREATE TABLE DroneSubTasks (
        DroneId UNIQUEIDENTIFIER NOT NULL,
        SubTaskId UNIQUEIDENTIFIER NOT NULL,
        AssignmentTime DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        IsActive BIT NOT NULL DEFAULT 1,  -- 当前有效分配

        -- 主键和约束
        PRIMARY KEY (DroneId, SubTaskId),
        FOREIGN KEY (DroneId) REFERENCES Drones(Id) ON DELETE CASCADE,
        FOREIGN KEY (SubTaskId) REFERENCES SubTasks(Id) ON DELETE CASCADE,

        -- 索引
        INDEX IX_DroneSubTasks_Time (AssignmentTime DESC),
        INDEX IX_DroneSubTasks_Active (IsActive),
        INDEX IX_DroneSubTasks_DroneActive (DroneId, IsActive),
        INDEX IX_DroneSubTasks_SubTaskActive (SubTaskId, IsActive)
    );
    GO

    -- 子任务历史表（核心变更记录）
    CREATE TABLE SubTaskHistory (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        SubTaskId UNIQUEIDENTIFIER NOT NULL,
        OldStatus TINYINT NULL,  -- 原状态
        NewStatus TINYINT NOT NULL,  -- 新状态
        ChangeTime DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ChangedBy NVARCHAR(128) NULL DEFAULT SUSER_SNAME(),  -- 操作者
        DroneId UNIQUEIDENTIFIER NULL,  -- 关联无人机
        Reason NVARCHAR(255) NULL,  -- 变更原因
        AdditionalInfo NVARCHAR(MAX) NULL,  -- 附加信息(JSON格式)

        -- 外键约束
        FOREIGN KEY (SubTaskId) REFERENCES SubTasks(Id) ON DELETE CASCADE,
        FOREIGN KEY (DroneId) REFERENCES Drones(Id),

        -- 索引
        INDEX IX_SubTaskHistory_SubTask (SubTaskId),
        INDEX IX_SubTaskHistory_ChangeTime (ChangeTime DESC),
        INDEX IX_SubTaskHistory_StatusChange (NewStatus, ChangeTime),
        INDEX IX_SubTaskHistory_DroneTime (DroneId, ChangeTime DESC)
    );
    GO
    -------------------------------------------
    -- 2. 自动记录历史触发器
    -------------------------------------------

    CREATE TRIGGER TR_SubTasks_StatusChange
    ON SubTasks
    AFTER UPDATE
    AS
    BEGIN
        SET NOCOUNT ON;

        -- 只处理状态变化的记录
        IF UPDATE(Status)
        BEGIN
            INSERT INTO SubTaskHistory (
                SubTaskId, 
                OldStatus, 
                NewStatus, 
                ChangeTime,
                ChangedBy,
                DroneId,
                Reason
            )
            SELECT 
                i.Id,
                d.Status,           -- 原状态
                i.Status,           -- 新状态
                GETUTCDATE(),
                SUSER_SNAME(),      -- 当前用户
                (SELECT TOP 1 DroneId 
                 FROM DroneSubTasks 
                 WHERE SubTaskId = i.Id 
                 AND IsActive = 1 
                 ORDER BY AssignmentTime DESC),  -- 当前分配的无人机
                CASE 
                    WHEN i.Status = 4 THEN '任务失败' 
                    ELSE '状态更新' 
                END
            FROM inserted i
            JOIN deleted d ON i.Id = d.Id
            WHERE i.Status <> d.Status; -- 仅状态变化时记录
        END
    END;
    GO

    -- 无人机-子任务关联变更触发器
    CREATE TRIGGER TR_DroneSubTasks_Assignment
    ON DroneSubTasks
    AFTER INSERT, UPDATE
    AS
    BEGIN
        SET NOCOUNT ON;

        -- 新分配任务时更新子任务状态
        UPDATE st
        SET st.Status = 1,  -- 更新为Assigned
            st.ReassignmentCount = st.ReassignmentCount + 1
        FROM SubTasks st
        JOIN inserted i ON st.Id = i.SubTaskId
        WHERE i.IsActive = 1;

        -- 记录状态变更历史
        INSERT INTO SubTaskHistory (
            SubTaskId, 
            OldStatus, 
            NewStatus, 
            ChangeTime,
            ChangedBy,
            DroneId,
            Reason
        )
        SELECT 
            i.SubTaskId,
            st.Status,  -- 原状态
            1,          -- 新状态: Assigned
            GETUTCDATE(),
            SUSER_SNAME(),
            i.DroneId,
            '任务分配'
        FROM inserted i
        JOIN SubTasks st ON i.SubTaskId = st.Id
        WHERE i.IsActive = 1;
    END;
    GO
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
