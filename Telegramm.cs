using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace GoProTimelapse
{
    public class Telegramm
    {
        private readonly TelegramBotClient _bot;
        private readonly AppDbContext _db;

        public Telegramm(string botToken)
        {
            _bot = new TelegramBotClient(botToken);
            _db = new AppDbContext();
        }

        //Запуск слушателя
        public async Task StartAsync()
        {
            var me = await _bot.GetMe();

            _bot.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync
            );

            Console.ReadLine();
        }

        //Основной обработчик сообщений
        private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken token)
        {
            if (update.Type != UpdateType.Message || update.Message == null)
                return;

            var message = update.Message;
            var chatId = (int)message.Chat.Id;

            switch (message.Text)
            {
                case "/start":
                    await HandleStartCommand(chatId, message);
                    break;

                case "/photo":
                    await HandlePhotoCommand(chatId, message);
                    break;

                case "/scheduledPhoto":
                    await CreateScheduledPhotoCommand(DateTime.UtcNow.AddMinutes(1), message, chatId);
                    break;

                default:
                    await bot.SendMessage(chatId, "Не понял команду");
                    break;
            }
        }

        //Обработка команды /start
        private async Task HandleStartCommand(int chatId, Message message)
        {
            var username = message.Chat.Username ?? $"user_{message.Chat.Id}";
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
            {
                user = new User
                {
                    Username = username,
                    FirstName = message.Chat.FirstName ?? "",
                    LastName = message.Chat.LastName ?? "",
                    RegisteredAt = DateTime.UtcNow,
                    TGUserId = chatId
                };

                _db.Users.Add(user);
                await _db.SaveChangesAsync();

                await _bot.SendMessage(chatId,
                    "👋 Привет! Ты зарегистрирован. Напиши /photo чтобы сделать тестовое фото.");
            }
            else
            {
                await _bot.SendMessage(chatId, "Ты уже зарегистрирован 😉");
            }
        }

        //Обработка команды /photo
        private async Task HandlePhotoCommand(int chatId, Message message)
        {
            var username = message.Chat.Username ?? $"user_{message.Chat.Id}";
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                await _bot.SendMessage(chatId, "⚠️ Сначала напиши /start, чтобы зарегистрироваться.");
                return;
            }

            var task = new TaskItem
            {
                Type = TaskType.Photo,
                Status = TaskStatus.Created,
                ChatId = chatId,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };
            _db.Tasks.Add(task);

            await _db.SaveChangesAsync();

            await _bot.SendMessage(chatId, "📸 Задача на фото создана. Сейчас обработаю!");
        }

        public async Task CreateScheduledPhotoCommand(DateTime scheduledTime, Message message, int chatId)
        {
            var username = message.Chat.Username ?? $"user_{message.Chat.Id}";
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                await _bot.SendMessage(chatId, "⚠️ Сначала напиши /start, чтобы зарегистрироваться.");
                return;
            }
            var task = new TaskItem
            {
                Type = TaskType.Photo,
                Status = TaskStatus.Created,
                CreatedAt = DateTime.UtcNow,
                ScheduledAt = scheduledTime
            };
            _db.Tasks.Add(task);
            await _db.SaveChangesAsync();
        }

        //Отправка видео
        public async Task SendVideo(string videoName, string botToken, int chatID)
        {
            using var cts = new CancellationTokenSource();
            var bot = new TelegramBotClient(botToken, cancellationToken: cts.Token);

            await using Stream stream = File.OpenRead($"./{videoName}");
            await bot.SendVideo(chatID, stream);
        }

        //Обработчик ошибок Telegram API
        private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken token)
        {
            Console.WriteLine($"Ошибка в боте: {exception.Message}");
            return Task.CompletedTask;
        }
    }

    //Расширение для простых сообщений
    public static class TelegramExtensions
    {
        public static async Task SendMessage(this ITelegramBotClient bot, int chatId, string text)
        {
            await bot.SendMessage(chatId, text, parseMode: ParseMode.Markdown);
        }
    }
}