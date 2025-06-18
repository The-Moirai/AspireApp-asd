using ClassLibrary_Core.Mission;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebApplication_Drone.Services;

namespace WebApplication_Drone.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TasksController : ControllerBase
    {
        private readonly TaskDataService _taskDataService;
        public TasksController(TaskDataService taskDataService)
        {
            _taskDataService = taskDataService;
        }
        [HttpGet]
        public ActionResult<IEnumerable<MainTask>> GetAll()
        {
            var mainTasks = _taskDataService.GetTasks();
            return Ok(mainTasks);
        }
        [HttpGet("{id:int}")]
        public ActionResult<MainTask> Get(Guid id)
        {
            var mainTask = _taskDataService.GetTask(id);
            return mainTask is not null ? Ok(mainTask) : NotFound();
        }
        [HttpPost("{CreateBy:string}")]
        public ActionResult<MainTask> Create(MainTask maintask,string CreateBy)
        {
            _taskDataService.AddTask(maintask,CreateBy);
            return CreatedAtAction(nameof(Get), new { id = maintask.Id }, maintask);
        }
        [HttpPost("upload")]
        public async Task<IActionResult> UploadTaskWithVideo([FromForm] string Description, [FromForm] Guid Id, [FromForm] DateTime CreationTime, [FromForm] IFormFile VideoFile)
        {
           var videosDir = Path.Combine(Directory.GetCurrentDirectory(), "TaskVideos");
           if (!Directory.Exists(videosDir))
           {
               Directory.CreateDirectory(videosDir);
           }

           // 保存视频文件
           var savePath = Path.Combine("TaskVideos", VideoFile.FileName);
           using (var stream = System.IO.File.Create(savePath))
           {
               await VideoFile.CopyToAsync(stream);
           }


            // 创建任务
            var task = new MainTask
            {
                Id = Id,
                Description = Description,
                CreationTime = CreationTime,
                // 其他字段按需补充
            };
            // 保存任务到数据源
            _taskDataService.AddTask(task,"User");

           return Ok();
        }
    }
}
