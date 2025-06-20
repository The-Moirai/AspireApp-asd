using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json;
using System.Net.Sockets;
using System.Text;

namespace BlazorApp_Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VideoAPIController : ControllerBase
    {
        private readonly ILogger<VideoAPIController> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _outputPath;
        private readonly string _pythonHost;
        private readonly int _pythonPort;

        public VideoAPIController(ILogger<VideoAPIController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _outputPath = Path.Combine("wwwroot", "processed");
            _pythonHost = "192.168.31.35"; // 默认 Python 后端主机
            _pythonPort = 5007; // 默认 Python 后端端口
            
            // 确保输出目录存在
            if (!Directory.Exists(_outputPath))
            {
                Directory.CreateDirectory(_outputPath);
            }
        }

        /// <summary>
        /// 获取Python后端的集群状态
        /// </summary>
        [HttpGet("cluster-status")]
        public async Task<IActionResult> GetClusterStatus()
        {
            try
            {
                var request = new
                {
                    type = "node_info",
                    content = "",
                    next_node = ""
                };

                var response = await SendToPythonBackend(JsonSerializer.Serialize(request));
                return Ok(new { success = true, data = response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取集群状态失败");
                return StatusCode(500, new { error = "获取集群状态失败", details = ex.Message });
            }
        }

        /// <summary>
        /// 启动指定数量的节点
        /// </summary>
        [HttpPost("start-nodes")]
        public async Task<IActionResult> StartNodes([FromBody] StartNodesRequest request)
        {
            try
            {
                var startRequest = new
                {
                    type = "start_all",
                    content = request.NodeCount,
                    next_node = ""
                };

                var response = await SendToPythonBackend(JsonSerializer.Serialize(startRequest));
                return Ok(new { success = true, message = $"成功启动 {request.NodeCount} 个节点", data = response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动节点失败");
                return StatusCode(500, new { error = "启动节点失败", details = ex.Message });
            }
        }

        /// <summary>
        /// 创建视频处理任务
        /// </summary>
        [HttpPost("create-task")]
        public async Task<IActionResult> CreateVideoProcessingTask([FromBody] CreateTaskRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.VideoPath) || string.IsNullOrEmpty(request.TaskName))
                {
                    return BadRequest("视频路径和任务名称不能为空");
                }

                // 检查视频文件是否存在
                if (!System.IO.File.Exists(request.VideoPath))
                {
                    return NotFound("视频文件不存在");
                }

                var createTaskRequest = new
                {
                    type = "create_tasks",
                    content = request.VideoPath,
                    next_node = request.TaskName
                };

                var response = await SendToPythonBackend(JsonSerializer.Serialize(createTaskRequest));
                
                return Ok(new 
                { 
                    success = true, 
                    taskId = request.TaskName,
                    message = "任务创建成功",
                    subtaskCount = 100, // real_work.py 默认分为100个子任务
                    response = response 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建视频处理任务失败");
                return StatusCode(500, new { error = "创建任务失败", details = ex.Message });
            }
        }

        /// <summary>
        /// 获取任务处理状态
        /// </summary>
        [HttpGet("task-status/{taskName}")]
        public async Task<IActionResult> GetTaskStatus(string taskName)
        {
            try
            {
                // 检查任务结果目录
                var taskPath = Path.Combine(_outputPath, taskName);
                var exists = Directory.Exists(taskPath);
                
                if (exists)
                {
                    var imageFiles = Directory.GetFiles(taskPath, "*.png").Length;
                    var isCompleted = imageFiles >= 100; // 假设100张图片为完成状态

                    return Ok(new
                    {
                        taskName = taskName,
                        status = isCompleted ? "completed" : "processing",
                        totalSubtasks = 100,
                        completedSubtasks = imageFiles,
                        processedImagePath = isCompleted ? taskPath : ""
                    });
                }
                else
                {
                    return Ok(new
                    {
                        taskName = taskName,
                        status = "processing",
                        totalSubtasks = 100,
                        completedSubtasks = 0,
                        processedImagePath = ""
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取任务状态失败");
                return StatusCode(500, new { error = "获取任务状态失败", details = ex.Message });
            }
        }

        /// <summary>
        /// 获取处理后的图片列表
        /// </summary>
        [HttpGet("processed-images/{taskName}")]
        public IActionResult GetProcessedImages(string taskName)
        {
            try
            {
                var taskPath = Path.Combine(_outputPath, taskName);
                
                if (!Directory.Exists(taskPath))
                {
                    return NotFound("任务结果不存在");
                }

                var imageFiles = Directory.GetFiles(taskPath, "*.png")
                    .OrderBy(f => f)
                    .Select(f => $"/processed/{taskName}/{Path.GetFileName(f)}")
                    .ToList();

                return Ok(new { images = imageFiles, count = imageFiles.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取处理后图片失败");
                return StatusCode(500, new { error = "获取图片失败", details = ex.Message });
            }
        }

        /// <summary>
        /// 将处理后的图片合成为视频
        /// </summary>
        [HttpPost("generate-video")]
        public async Task<IActionResult> GenerateVideo([FromBody] GenerateVideoRequest request)
        {
            try
            {
                var taskPath = Path.Combine(_outputPath, request.TaskName);
                
                if (!Directory.Exists(taskPath))
                {
                    return NotFound("任务结果不存在");
                }

                var imageFiles = Directory.GetFiles(taskPath, "*.png").OrderBy(f => f).ToArray();
                
                if (imageFiles.Length == 0)
                {
                    return BadRequest("没有找到处理后的图片");
                }

                var outputVideoPath = Path.Combine(taskPath, "output.mp4");
                
                // 使用 FFmpeg 生成视频
                await GenerateVideoFromImages(taskPath, outputVideoPath, request.FrameRate);

                if (System.IO.File.Exists(outputVideoPath))
                {
                    return Ok(new
                    {
                        success = true,
                        videoUrl = $"/processed/{request.TaskName}/output.mp4",
                        message = "视频生成成功"
                    });
                }
                else
                {
                    return StatusCode(500, new { error = "视频生成失败" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成视频失败");
                return StatusCode(500, new { error = "生成视频失败", details = ex.Message });
            }
        }

        /// <summary>
        /// 关闭指定节点
        /// </summary>
        [HttpPost("shutdown-node")]
        public async Task<IActionResult> ShutdownNode([FromBody] ShutdownNodeRequest request)
        {
            try
            {
                var shutdownRequest = new
                {
                    type = "shutdown",
                    content = request.NodeName,
                    next_node = ""
                };

                await SendToPythonBackend(JsonSerializer.Serialize(shutdownRequest));
                
                return Ok(new { success = true, message = $"节点 {request.NodeName} 关闭请求已发送" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "关闭节点失败");
                return StatusCode(500, new { error = "关闭节点失败", details = ex.Message });
            }
        }

        /// <summary>
        /// 向Python后端发送消息
        /// </summary>
        private async Task<string> SendToPythonBackend(string message)
        {
            using var client = new TcpClient();
            await client.ConnectAsync(_pythonHost, _pythonPort);
            
            var data = Encoding.UTF8.GetBytes(message);
            var stream = client.GetStream();
            await stream.WriteAsync(data, 0, data.Length);

            // 接收响应
            var buffer = new byte[4096];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            return response;
        }

        /// <summary>
        /// 使用FFmpeg从图片生成视频
        /// </summary>
        private async Task GenerateVideoFromImages(string imagesPath, string outputVideoPath, int frameRate = 30)
        {
            var ffmpegArgs = $"-y -r {frameRate} -i \"{imagesPath}/%04d.png\" -c:v libx264 -pix_fmt yuv420p \"{outputVideoPath}\"";
            
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = ffmpegArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                
                if (process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    throw new Exception($"FFmpeg 失败: {error}");
                }
            }
        }
    }

    // 请求模型
    public class StartNodesRequest
    {
        public int NodeCount { get; set; }
    }

    public class CreateTaskRequest
    {
        public string VideoPath { get; set; } = "";
        public string TaskName { get; set; } = "";
        public string ProcessingType { get; set; } = "mixed";
    }

    public class GenerateVideoRequest
    {
        public string TaskName { get; set; } = "";
        public int FrameRate { get; set; } = 30;
    }

    public class ShutdownNodeRequest
    {
        public string NodeName { get; set; } = "";
    }
} 