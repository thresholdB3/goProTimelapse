using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Telegram.Bot;
using Serilog;
using Serilog.Events;

namespace GoProTimelapse
{
    public class Worker
    {
        private readonly AppDbContext _db;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0, 1);
        // private readonly Settings _settings;//settings тут вроде вообще не используется, лол
        private static readonly ILogger Log = Serilog.Log.ForContext<Worker>();

        public Worker(Settings settings)
        {
            _db = new AppDbContext();
            // _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public static async Task NotifyNewTask()
        {
            _semaphore.Release();
            Log.Debug("Уведомление о новой задаче");
        }

        public async Task StartAsync(CancellationToken token)
        {
            Log.Information("Запуск воркера...");
            while (!token.IsCancellationRequested)
            {
                await _semaphore.WaitAsync(token);
                await ProcessPendingTasks();
            }
        }

        private async Task ProcessPendingTasks()
        {
            Log.Debug("Обработка задачи...");
            try
            {
                var newTasks = await _db.Tasks
                    .Where(t => t.Status == TaskStatus.Created)
                    .ToListAsync();

                foreach (var task in newTasks)
                {
                    task.Status = TaskStatus.InProgress;
                    task.StartedAt = DateTimeOffset.Now;
                    await _db.SaveChangesAsync();

                    if (task.Type == TaskType.Photo)
                    {
                        if (task.ScheduledAt != null)
                        {
                            _ = Task.Run(async () =>
                            {
                                var photoDelay = task.ScheduledAt.Value - DateTimeOffset.Now;
                                await Task.Delay(photoDelay);
                                Log.Debug("Фото отложено на {PhotoDelay} милисекунд", photoDelay);
                                await HandlePhotoTask(task);
                            });
                        }
                        else
                        {
                            await HandlePhotoTask(task);
                        }
                    }else if (task.Type == TaskType.Timelapse)
                    {
                        var timelapseDelay = task.ScheduledAt.Value - DateTimeOffset.Now;
                        Log.Debug("Таймлапс отложен на {TimelapseDelay} милисекунд", timelapseDelay);
                        if (timelapseDelay.TotalMilliseconds < 0) //чтобы не падало, потом покрасивше сделать
                        {
                            await Task.Delay(0);
                        }
                        else
                        {
                            await Task.Delay(timelapseDelay);
                            //await Task.Delay(0);
                        }
                        await HandleTimelapse(task);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обработке задачи");
            }
        }

        private async Task HandlePhotoTask(TaskItem task)
        {
            Log.Debug("Обработка фото...");
            try
            {
                await new ProcessPhoto().Execute(new ProcessPhotoArgs(task) );
                
                task.Status = TaskStatus.Completed;
                task.FinishedAt = DateTimeOffset.Now;
                await _db.SaveChangesAsync();

                Log.Debug("Фото обработано!");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обработке фото");
            }
        }

        private async Task HandleTimelapse(TaskItem task)
        {
            Log.Debug("Обработка таймлапса...");
            try
            {
                var subscribedUsers = await _db.Users
                    .Where(u => u.SunsetSubscribtion == true)
                    .ToListAsync();
                Log.Debug("Пользователи с подпиской найдены");

                List<long> userIdList = new List<long>();

                foreach (var user in subscribedUsers)
                {
                    userIdList.Add(user.TGUserId);
                    Log.Debug("Добавлен пользователь {user.Username}", user.Username);
                }

                await new ProcessTimelapse().Execute(new ProcessTimelapseArgs(task.Parameters, userIdList));

                task.Status = TaskStatus.Completed;
                task.FinishedAt = DateTimeOffset.Now;
                await _db.SaveChangesAsync();
                Log.Debug("Таймлапс обработан :)");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обработке таймлапса :(");
            }
        }
    }
}
