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
        private static readonly ILogger Log = Serilog.Log.ForContext<Worker>();

        public Worker(Settings settings)
        {
            _db = new AppDbContext();
        }

        public static async Task NotifyNewTask()
        {
            _semaphore.Release();
            Log.Information("Уведомление о новой задаче");
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
            Log.Information("Обработка задачи...");
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
                                Log.Information("Фото отложено на {PhotoDelay} милисекунд", photoDelay);
                                await HandlePhotoTask(task);
                            });
                        }
                        else
                        {
                            await HandlePhotoTask(task);
                        }
                    }
                    else if (task.Type == TaskType.Timelapse)
                    {
                        _ = Task.Run(async () =>
                        {
                            var timelapseDelay = task.ScheduledAt.Value - DateTimeOffset.Now;
                            Log.Information("Таймлапс отложен на {TimelapseDelay} милисекунд", timelapseDelay);
                            await Task.Delay(timelapseDelay);
                            await HandleTimelapse(task);
                        });
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
                Log.Information("Таймлапс обработан :)");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обработке таймлапса :(");
            }
        }
    }
}
