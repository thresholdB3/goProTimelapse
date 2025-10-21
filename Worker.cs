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

        public Worker(string botToken, Settings settings)
        {
            _db = new AppDbContext();
            _bot = new TelegramBotClient(botToken);
            _camera = new GoProCameraFake(settings);
        }

        public async Task StartAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await ProcessPendingTasks();
                await Task.Delay(2000, token);
            }
        }

        private async Task ProcessPendingTasks()
        {
            var newTasks = await _db.Tasks
                .Where(t => t.Status == TaskStatus.Created)
                .ToListAsync();

            foreach (var task in newTasks)
            {
                task.Status = TaskStatus.InProgress;
                task.StartedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                if (task.Type == TaskType.Photo)
                    await HandlePhotoTask(task);

                task.Status = TaskStatus.Completed;
                task.FinishedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }

        private async Task HandlePhotoTask(TaskItem task)
        {
            //Достаём chatId из JSON в Parameters
            long chatId;
            var parameters = JsonDocument.Parse(task.Parameters);
            chatId = parameters.RootElement.GetProperty("chatId").GetInt64();

            var user = await _db.Users.FindAsync(task.UserId);

            await _camera.SetPhotoModeAsync();
            await _camera.TakePhotoAsync();

            Console.WriteLine($"Отправка фото пользователю {user.Username}");

            await using var stream = File.OpenRead(@"GoProPhotos\0.jpg");
            await _bot.SendPhoto(chatId, stream, caption: "📸 Вот твоё фото!");
        }
    }
}
