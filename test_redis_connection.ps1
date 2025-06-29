# Redis连接测试脚本
# 用于快速验证Redis连接状态

Write-Host "🔍 Redis连接问题排查工具" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan

# 1. 检查Docker容器状态
Write-Host "`n1. 检查Docker容器状态..." -ForegroundColor Yellow
try {
    $redisContainer = docker ps --filter "name=redis" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
    if ($redisContainer -and $redisContainer -notlike "*Empty*") {
        Write-Host "✅ Redis容器运行中:" -ForegroundColor Green
        Write-Host $redisContainer
    } else {
        Write-Host "❌ 未找到运行中的Redis容器" -ForegroundColor Red
        Write-Host "💡 请检查Aspire应用是否正在运行" -ForegroundColor Yellow
    }
} catch {
    Write-Host "❌ 无法检查Docker状态: $($_.Exception.Message)" -ForegroundColor Red
}

# 2. 测试网络连接
Write-Host "`n2. 测试网络连接..." -ForegroundColor Yellow
try {
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $connectTask = $tcpClient.ConnectAsync("cache", 6379)
    
    if ($connectTask.Wait(5000)) {
        if ($tcpClient.Connected) {
            Write-Host "✅ 网络连接成功" -ForegroundColor Green
            $tcpClient.Close()
        } else {
            Write-Host "❌ 网络连接失败" -ForegroundColor Red
        }
    } else {
        Write-Host "❌ 网络连接超时" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ 网络连接测试失败: $($_.Exception.Message)" -ForegroundColor Red
}

# 3. 检查端口监听
Write-Host "`n3. 检查端口监听..." -ForegroundColor Yellow
try {
    $listeningPorts = netstat -an | Select-String ":6379"
    if ($listeningPorts) {
        Write-Host "✅ 6379端口正在监听:" -ForegroundColor Green
        $listeningPorts | ForEach-Object { Write-Host "   $_" }
    } else {
        Write-Host "❌ 6379端口未监听" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ 无法检查端口监听: $($_.Exception.Message)" -ForegroundColor Red
}

# 4. 测试Redis CLI连接
Write-Host "`n4. 测试Redis CLI连接..." -ForegroundColor Yellow
try {
    $pingResult = docker exec $(docker ps -q --filter "name=redis") redis-cli ping 2>$null
    if ($pingResult -eq "PONG") {
        Write-Host "✅ Redis CLI连接成功" -ForegroundColor Green
    } else {
        Write-Host "❌ Redis CLI连接失败: $pingResult" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ Redis CLI连接测试失败: $($_.Exception.Message)" -ForegroundColor Red
}

# 5. 检查配置文件
Write-Host "`n5. 检查配置文件..." -ForegroundColor Yellow
$configFiles = @(
    "WebApplication_Drone/appsettings.json",
    "WebApplication_Drone/appsettings.Development.json"
)

foreach ($file in $configFiles) {
    if (Test-Path $file) {
        Write-Host "📄 检查配置文件: $file" -ForegroundColor Blue
        $content = Get-Content $file -Raw
        if ($content -match '"cache"') {
            Write-Host "✅ 找到cache配置" -ForegroundColor Green
        } else {
            Write-Host "❌ 未找到cache配置" -ForegroundColor Red
        }
    } else {
        Write-Host "❌ 配置文件不存在: $file" -ForegroundColor Red
    }
}

# 6. 检查Aspire配置
Write-Host "`n6. 检查Aspire配置..." -ForegroundColor Yellow
$aspireFile = "AspireApp.AppHost/Program.cs"
if (Test-Path $aspireFile) {
    $content = Get-Content $aspireFile -Raw
    if ($content -match "AddRedis") {
        Write-Host "✅ Aspire Redis配置正确" -ForegroundColor Green
    } else {
        Write-Host "❌ Aspire Redis配置缺失" -ForegroundColor Red
    }
} else {
    Write-Host "❌ Aspire配置文件不存在" -ForegroundColor Red
}

# 7. 提供诊断建议
Write-Host "`n7. 诊断建议..." -ForegroundColor Yellow
Write-Host "📋 如果发现问题，请尝试以下步骤:" -ForegroundColor Cyan
Write-Host "   1. 重启Aspire应用: dotnet run --project AspireApp.AppHost" -ForegroundColor White
Write-Host "   2. 检查Docker Desktop是否运行" -ForegroundColor White
Write-Host "   3. 使用API诊断工具: POST /api/redisdiagnostic/diagnose" -ForegroundColor White
Write-Host "   4. 查看应用日志获取详细错误信息" -ForegroundColor White
Write-Host "   5. 检查防火墙设置" -ForegroundColor White

Write-Host "`n🔧 可用的诊断API端点:" -ForegroundColor Cyan
Write-Host "   POST /api/redisdiagnostic/quick-test    - 快速连接测试" -ForegroundColor White
Write-Host "   POST /api/redisdiagnostic/diagnose      - 完整诊断" -ForegroundColor White
Write-Host "   GET  /api/redisdiagnostic/stats         - 连接统计" -ForegroundColor White
Write-Host "   POST /api/redisdiagnostic/stress-test   - 压力测试" -ForegroundColor White
Write-Host "   GET  /api/redisdiagnostic/errors        - 错误详情" -ForegroundColor White

Write-Host "`n✅ 诊断完成!" -ForegroundColor Green 