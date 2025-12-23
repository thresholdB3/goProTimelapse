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
        private readonly GoProCameraFake _camera; //тут камеры быть не должно
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0, 1);
        private readonly Settings _settings;
        private static readonly ILogger Log = Serilog.Log.ForContext<Worker>();

        public Worker(string botToken, Settings settings)
        {
            _db = new AppDbContext();
            _camera = new GoProCameraFake();
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
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
                await new ProcessPhoto().Execute();

                await using var stream = File.OpenRead(@"GoProPhotos\1.jpg");

                Log.Debug("Отправка фото пользователю {task.ChatId}", task.ChatId);

                await Telegramm.SendPhoto(task.ChatId, stream, "📸 Вот твоё фото!");

                
                
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
                await using var stream = File.OpenRead(@"GoProPhotos\1.jpg");
                var subscribedUsers = await _db.Users
                    .Where(u => u.SunsetSubscribtion == true)
                    .ToListAsync();

                foreach (var user in subscribedUsers)
                {
                    await Telegramm.SendPhoto(user.TGUserId, stream, "📸 Запланированное фото!");

                    Log.Debug("Отправлено фото пользователю {user.Username}", user.Username);
                }
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
                // string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..\\..\\.."));
                // string outputFile = Path.Combine(projectRoot, DateTime.Now.ToString("ssmmhh.ddMMyyyy") + ".mp4");

                // string outputFile = @"GoProPhotos\1.jpg";
                // await using var stream = File.OpenRead(outputFile);

                var subscribedUsers = await _db.Users
                    .Where(u => u.SunsetSubscribtion == true)
                    .ToListAsync();
                Log.Debug("Пользователи с подпиской найдены");

                List<long> userId = new List<long>();

                foreach (var user in subscribedUsers)
                {
                    // await Telegramm.SendPhoto(user.TGUserId, stream, "Крутой таймлапс!");
                    userId.Add(user.TGUserId);
                    Log.Debug("Добавлен пользователь {user.Username}", user.Username);
                }

                var parametersJson = new 
                {
                    Users = userId,
                    TimelapseDelay = task.Parameters
                };
                var parameters = JsonSerializer.Serialize(parametersJson);//потом путь принимает сразу json наверное

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
