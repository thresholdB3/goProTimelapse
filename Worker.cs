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
            Console.WriteLine("Воркeр запущен");

            while (!token.IsCancellationRequested)
            {
                await ProcessPendingTasks();
                await Task.Delay(TimeSpan.FromSeconds(2), token);
            }
        }

        private async Task ProcessPendingTasks()
        {
            var newTasks = await _db.Tasks
                .Where(t => t.Status == TaskStatus.Created)
                .ToListAsync();

            foreach (var task in newTasks)
            {
                Console.WriteLine($"Обработка задачи #{task.Id} ({task.Type})...");

                task.Status = TaskStatus.InProgress;
                task.StartedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                try
                {
                    if (task.Type == TaskType.Photo)
                        await HandlePhotoTask(task);

                    task.Status = TaskStatus.Completed;
                    task.FinishedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();

                    Console.WriteLine($"Задача #{task.Id} выполнена");
                }
                catch (Exception ex)
                {
                    task.Status = TaskStatus.Failed;
                    await _db.SaveChangesAsync();
                    Console.WriteLine($"Ошибка при выполнении задачи #{task.Id}: {ex.Message}");
                }
            }
        }

        private async Task HandlePhotoTask(TaskItem task)
        {
            //Достаём chatId из JSON в Parameters
            long chatId;
            try
            {
                var parameters = JsonDocument.Parse(task.Parameters);
                chatId = parameters.RootElement.GetProperty("chatId").GetInt64();
            }
            catch (Exception)
            {
                Console.WriteLine($"Невозможно прочитать chatId у задачи #{task.Id}");
                return;
            }

            var user = await _db.Users.FindAsync(task.UserId);
            if (user == null)
            {
                Console.WriteLine($"Не найден пользователь для задачи #{task.Id}");
                return;
            }

            await _camera.SetPhotoModeAsync();
            await _camera.TakePhotoAsync();

            Console.WriteLine($"Отправка фото пользователю {user.Username}");

            await using var stream = File.OpenRead(@"GoProPhotos\0.jpg");
            await _bot.SendPhoto(chatId, stream, caption: "📸 Вот твоё фото!");
        }
    }
}
