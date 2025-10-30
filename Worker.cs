using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Telegram.Bot;

namespace GoProTimelapse
{
    public class Worker
    {
        private readonly AppDbContext _db;
        private readonly TelegramBotClient _bot;
        private readonly GoProCameraFake _camera;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0, 1);

        public Worker(string botToken, Settings settings)
        {
            _db = new AppDbContext();
            _bot = new TelegramBotClient(botToken);
            _camera = new GoProCameraFake(settings);
        }

        public static async Task NotifyNewTask()
        {
            _semaphore.Release();
        }

        public async Task StartAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await _semaphore.WaitAsync(token);
                Console.WriteLine("ogo");
                await ProcessPendingTasks();
            }
        }

        private async Task ProcessPendingTasks()
        {
            Console.WriteLine("ogo2");
            var newTasks = await _db.Tasks
                .Where(t => t.Status == TaskStatus.Created)
                .ToListAsync();

            foreach (var task in newTasks)
            {
                task.Status = TaskStatus.InProgress;
                task.StartedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                if (task.Type == TaskType.Photo)
                {
                    if (task.ScheduledAt != null)
                    {
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(task.ScheduledAt.Value - DateTime.UtcNow);
                            await HandleScheduledPhotoTask(task);
                        });
                    }
                    else
                    {
                        await HandlePhotoTask(task);
                    }
                }
            }
        }

        private async Task HandlePhotoTask(TaskItem task)
        {
            await using var stream = File.OpenRead(@"GoProPhotos\345.jpg");

            var user = await _db.Users.FindAsync(task.UserId);

            await _camera.SetPhotoModeAsync();
            await _camera.TakePhotoAsync();

            Console.WriteLine($"Отправка фото пользователю {task.ChatId}");

            await _bot.SendPhoto(task.ChatId, stream, caption: "📸 Вот твоё фото!");
            task.Status = TaskStatus.Completed;
            task.FinishedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        private async Task HandleScheduledPhotoTask(TaskItem task)
        {
            await using var stream = File.OpenRead(@"GoProPhotos\345.jpg");
            var subscribedUsers = await _db.Users
                .Where(u => u.SunsetSubscribtion == true) //ПОМЕНЯТЬ НА TRUEEE!!!! 
                .ToListAsync();

            foreach (var user in subscribedUsers)
            {
                await _bot.SendPhoto(user.TGUserId, stream, caption: "📸 Запланированное фото!");
                Console.WriteLine($"Отправлено фото пользователю {user.Username}");
            }
            task.Status = TaskStatus.Completed;
            task.FinishedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}
