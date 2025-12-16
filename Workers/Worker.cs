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
        private readonly TelegramBotClient _bot;
        private readonly GoProCameraFake _camera;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0, 1);
        private readonly Settings _settings;
        private static readonly ILogger Log = Serilog.Log.ForContext<Worker>();
        // telegramBot = Telegramm.CreateSingleton(settings.Telegramm.botToken);

        public Worker(string botToken, Settings settings)
        {
            _db = new AppDbContext();
            _bot = new TelegramBotClient(botToken);
            _camera = new GoProCameraFake(settings);
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
                await using var stream = File.OpenRead(@"GoProPhotos\1.jpg");

                // await _camera.SetPhotoModeAsync();
                // await _camera.TakePhotoAsync();

                await _camera.TakePhoto();

                Log.Debug("Отправка фото пользователю {task.ChatId}", task.ChatId);

                // await _bot.SendPhoto(task.ChatId, stream, caption: "📸 Вот твоё фото!");
                // telegramBot.SendPhoto(task.ChatId, stream, "📸 Вот твоё фото!");
                // Telegramm.SendPhoto(task.ChatId, stream, "📸 Вот твоё фото!");
                await Telegramm
                    .CreateSingleton("") // токен тут НЕ используется, экземпляр уже есть
                    .SendPhoto(task.ChatId, stream, "📸 Вот твоё фото!");
                
                
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
                    // await _bot.SendPhoto(user.TGUserId, stream, caption: "📸 Запланированное фото!");
                    await Telegramm
                        .CreateSingleton("")
                        .SendPhoto(user.TGUserId, stream, "📸 Запланированное фото!");
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

                // await FFMpegWorker.CreateVideoFromPhotos(_settings.Base.DownloadFolder, outputFile);
                await _camera.StartTimeLapse();
                // _camera.isBusy = true;

                var timelapseDelay = (int)TimeSpan.Parse(task.Parameters).TotalMilliseconds; //потом сделать нормально
                Log.Debug("Время съёмки в милисекундах: {TimelapseDelay}", timelapseDelay); 
                await Task.Delay(timelapseDelay);
                await _camera.StopTimeLapse(); //надо будет передавать длительность в параметрах задачи
                // _camera.isBusy = false;

                string outputFile = @"GoProPhotos\1.jpg";
                await using var stream = File.OpenRead(outputFile);

                var subscribedUsers = await _db.Users
                    .Where(u => u.SunsetSubscribtion == true)
                    .ToListAsync();
                Log.Debug("Пользователи с подпиской найдены");

                foreach (var user in subscribedUsers)
                {
                    Console.WriteLine(user.TGUserId);
                    // await _bot.SendPhoto(user.TGUserId, outputFile, caption: "Крутой таймлапс!");
                    await Telegramm
                        .CreateSingleton("")
                        .SendPhoto(user.TGUserId, stream, "Крутой таймлапс!");
                    Log.Debug("Отправлен таймлапс пользователю {user.Username}", user.Username);
                }
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
