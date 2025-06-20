using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json;

namespace BlazorApp_Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImageProcessingController : ControllerBase
    {
        private readonly ILogger<ImageProcessingController> _logger;
        private readonly string _pythonPath;
        private readonly string _scriptPath;
        private readonly string _outputPath;

        public ImageProcessingController(ILogger<ImageProcessingController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _pythonPath = configuration.GetValue<string>("Python:ExecutablePath") ?? "python";
            _scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "linux_code");
            _outputPath = Path.Combine("wwwroot", "processed");
            
            // 确保输出目录存在
            if (!Directory.Exists(_outputPath))
            {
                Directory.CreateDirectory(_outputPath);
            }
        }

        [HttpPost("process-video")]
        public async Task<IActionResult> ProcessVideo([FromForm] ProcessVideoRequest request)
        {
            try
            {
                if (request.VideoFile == null || request.VideoFile.Length == 0)
                {
                    return BadRequest("没有上传视频文件");
                }

                // 保存上传的视频文件
                var videoFileName = $"{Guid.NewGuid()}.mp4";
                var videoPath = Path.Combine(_outputPath, videoFileName);
                
                using (var stream = new FileStream(videoPath, FileMode.Create))
                {
                    await request.VideoFile.CopyToAsync(stream);
                }

                var result = new ProcessingResult
                {
                    TaskId = Guid.NewGuid().ToString(),
                    Status = "processing",
                    Message = "视频处理已开始",
                    VideoFileName = videoFileName
                };

                // 启动后台处理任务
                _ = Task.Run(() => ProcessVideoInBackground(result.TaskId, videoPath, request.ProcessingType));

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理视频时发生错误");
                return StatusCode(500, new { error = "服务器内部错误", details = ex.Message });
            }
        }

        [HttpGet("status/{taskId}")]
        public IActionResult GetProcessingStatus(string taskId)
        {
            var statusFile = Path.Combine(_outputPath, $"{taskId}_status.json");
            
            if (!System.IO.File.Exists(statusFile))
            {
                return NotFound("任务不存在");
            }

            var statusJson = System.IO.File.ReadAllText(statusFile);
            var status = JsonSerializer.Deserialize<ProcessingResult>(statusJson);
            
            return Ok(status);
        }

        [HttpGet("results/{taskId}")]
        public IActionResult GetProcessingResults(string taskId)
        {
            var resultFile = Path.Combine(_outputPath, $"{taskId}_result.json");
            
            if (!System.IO.File.Exists(resultFile))
            {
                return NotFound("结果不存在");
            }

            var resultJson = System.IO.File.ReadAllText(resultFile);
            var result = JsonSerializer.Deserialize<ProcessingResultData>(resultJson);
            
            return Ok(result);
        }

        [HttpGet("processed-images/{taskId}")]
        public IActionResult GetProcessedImages(string taskId)
        {
            var imagesPath = Path.Combine(_outputPath, taskId, "images");
            
            if (!Directory.Exists(imagesPath))
            {
                return NotFound("处理后的图片不存在");
            }

            var imageFiles = Directory.GetFiles(imagesPath, "*.png")
                .OrderBy(f => f)
                .Select(f => $"/processed/{taskId}/images/{Path.GetFileName(f)}")
                .ToList();

            return Ok(new { images = imageFiles });
        }

        [HttpGet("output-video/{taskId}")]
        public IActionResult GetOutputVideo(string taskId)
        {
            var videoPath = Path.Combine(_outputPath, taskId, "output.mp4");
            
            if (!System.IO.File.Exists(videoPath))
            {
                return NotFound("输出视频不存在");
            }

            return Ok(new { videoUrl = $"/processed/{taskId}/output.mp4" });
        }

        private async Task ProcessVideoInBackground(string taskId, string videoPath, string processingType)
        {
            var taskOutputPath = Path.Combine(_outputPath, taskId);
            var imagesOutputPath = Path.Combine(taskOutputPath, "images");
            
            try
            {
                // 创建任务输出目录
                if (!Directory.Exists(taskOutputPath))
                {
                    Directory.CreateDirectory(taskOutputPath);
                }
                if (!Directory.Exists(imagesOutputPath))
                {
                    Directory.CreateDirectory(imagesOutputPath);
                }

                // 更新状态为处理中
                await UpdateTaskStatus(taskId, new ProcessingResult
                {
                    TaskId = taskId,
                    Status = "processing",
                    Message = "正在分析视频...",
                    Progress = 10
                });

                // 调用 Python 脚本进行图像处理
                var pythonScript = processingType switch
                {
                    "face" => "process_faces.py",
                    "object" => "process_objects.py",
                    _ => "process_mixed.py"
                };

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = _pythonPath,
                    Arguments = $"{Path.Combine(_scriptPath, pythonScript)} \"{videoPath}\" \"{imagesOutputPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                await UpdateTaskStatus(taskId, new ProcessingResult
                {
                    TaskId = taskId,
                    Status = "processing",
                    Message = "正在处理图像...",
                    Progress = 30
                });

                using var process = Process.Start(processStartInfo);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        await UpdateTaskStatus(taskId, new ProcessingResult
                        {
                            TaskId = taskId,
                            Status = "processing",
                            Message = "正在生成输出视频...",
                            Progress = 70
                        });

                        // 生成输出视频
                        await GenerateOutputVideo(taskId, imagesOutputPath);

                        // 完成处理
                        await UpdateTaskStatus(taskId, new ProcessingResult
                        {
                            TaskId = taskId,
                            Status = "completed",
                            Message = "处理完成",
                            Progress = 100
                        });

                        // 保存处理结果
                        await SaveProcessingResults(taskId, imagesOutputPath);
                    }
                    else
                    {
                        _logger.LogError($"Python script failed: {error}");
                        await UpdateTaskStatus(taskId, new ProcessingResult
                        {
                            TaskId = taskId,
                            Status = "failed",
                            Message = $"处理失败: {error}",
                            Progress = 0
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Background processing failed for task {taskId}");
                await UpdateTaskStatus(taskId, new ProcessingResult
                {
                    TaskId = taskId,
                    Status = "failed",
                    Message = $"处理异常: {ex.Message}",
                    Progress = 0
                });
            }
        }

        private async Task GenerateOutputVideo(string taskId, string imagesPath)
        {
            var outputVideoPath = Path.Combine(_outputPath, taskId, "output.mp4");
            var imageFiles = Directory.GetFiles(imagesPath, "*.png").OrderBy(f => f).ToArray();

            if (imageFiles.Length == 0)
            {
                throw new Exception("没有找到处理后的图片");
            }

            // 使用 FFmpeg 生成视频
            var ffmpegArgs = $"-y -r 30 -i \"{imagesPath}/%04d.png\" -c:v libx264 -pix_fmt yuv420p \"{outputVideoPath}\"";
            
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

        private async Task UpdateTaskStatus(string taskId, ProcessingResult status)
        {
            var statusFile = Path.Combine(_outputPath, $"{taskId}_status.json");
            var statusJson = JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(statusFile, statusJson);
        }

        private async Task SaveProcessingResults(string taskId, string imagesPath)
        {
            var imageFiles = Directory.GetFiles(imagesPath, "*.png")
                .OrderBy(f => f)
                .Select(f => $"/processed/{taskId}/images/{Path.GetFileName(f)}")
                .ToList();

            var result = new ProcessingResultData
            {
                TaskId = taskId,
                ProcessedImages = imageFiles,
                OutputVideo = $"/processed/{taskId}/output.mp4",
                ImageCount = imageFiles.Count,
                ProcessingTime = DateTime.Now
            };

            var resultFile = Path.Combine(_outputPath, $"{taskId}_result.json");
            var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(resultFile, resultJson);
        }
    }

    public class ProcessVideoRequest
    {
        public IFormFile VideoFile { get; set; } = null!;
        public string ProcessingType { get; set; } = "mixed"; // face, object, mixed
    }

    public class ProcessingResult
    {
        public string TaskId { get; set; } = "";
        public string Status { get; set; } = ""; // processing, completed, failed
        public string Message { get; set; } = "";
        public int Progress { get; set; } = 0;
        public string VideoFileName { get; set; } = "";
    }

    public class ProcessingResultData
    {
        public string TaskId { get; set; } = "";
        public List<string> ProcessedImages { get; set; } = new();
        public string OutputVideo { get; set; } = "";
        public int ImageCount { get; set; }
        public DateTime ProcessingTime { get; set; }
    }
} 