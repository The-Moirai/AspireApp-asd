-- 正确的SQL查询语法 - GUID值必须用单引号包围
SELECT Id, Description, Status, CreationTime, AssignedTime, CompletedTime, ParentTask, ReassignmentCount, AssignedDrone 
FROM SubTasks 
WHERE ParentTask = '4a36f861-dc58-413a-b2a6-5d69a8fc8ee9' 
AND Description = '4a36f861-dc58-413a-b2a6-5d69a8fc8ee9_0_0';

-- 如果您需要查询所有相关的子任务，可以使用：
SELECT Id, Description, Status, CreationTime, AssignedTime, CompletedTime, ParentTask, ReassignmentCount, AssignedDrone 
FROM SubTasks 
WHERE ParentTask = '4a36f861-dc58-413a-b2a6-5d69a8fc8ee9' 
ORDER BY CreationTime;

-- 如果您需要使用模糊匹配查找以特定前缀开头的子任务：
SELECT Id, Description, Status, CreationTime, AssignedTime, CompletedTime, ParentTask, ReassignmentCount, AssignedDrone 
FROM SubTasks 
WHERE ParentTask = '4a36f861-dc58-413a-b2a6-5d69a8fc8ee9' 
AND Description LIKE '4a36f861-dc58-413a-b2a6-5d69a8fc8ee9_0_%'
ORDER BY Description; 