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
                                await HandleScheduledPhotoTask(task);
                            });
                        }
                        else
                        {
                            await HandlePhotoTask(task);
                        }
                    }else if (task.Type == TaskType.Timelapse)
                    {
                        var timelapseDelay = task.ScheduledAt.Value - DateTimeOffset.Now;
                        // await Task.Delay(timelapseDelay);
                        Log.Debug("Таймлапс отложен(нет) на {TimelapseDelay} милисекунд", timelapseDelay);
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
                var parametersJson = new 
                {
                    UserId = task.ChatId,
                    Delay = 0
                };
                var parameters = JsonSerializer.Serialize(parametersJson);

                await new ProcessPhoto().Execute(parameters);
                
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

        private async Task HandleScheduledPhotoTask(TaskItem task)
        {
            Log.Debug("Обработка запланированного фото...");
            try
            {
                var parametersJson = new 
                {
                    UserId = task.ChatId,
                    Delay = task.Parameters
                };
                var parameters = JsonSerializer.Serialize(parametersJson);
                
                await new ProcessPhoto().Execute(parameters);

                task.Status = TaskStatus.Completed;
                task.FinishedAt = DateTimeOffset.Now;
                await _db.SaveChangesAsync();
                Log.Debug("Запланированное фото обработано!");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обработке запланированного фото");
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

                List<long> userId = new List<long>();

                foreach (var user in subscribedUsers)
                {
                    userId.Add(user.TGUserId);
                    Log.Debug("Добавлен пользователь {user.Username}", user.Username);
                }

                var parametersJson = new 
                {
                    Users = userId,
                    TimelapseDelay = task.Parameters
                };
                var parameters = JsonSerializer.Serialize(parametersJson);//потом путь принимает сразу json наверное
                                                                            //НЕТТТ остальным методам json не нужен, так проще

                await new ProcessTimelapse().Execute(parameters);

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
