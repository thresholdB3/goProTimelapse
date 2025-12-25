using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.EntityFrameworkCore;
using System.IO;
using Serilog;
using Serilog.Events;
using System.Diagnostics;
using Telegram.CalendarKit;
using Telegram.Bot.Extensions;


namespace GoProTimelapse
{
    public class Telegramm
    {
        private readonly TelegramBotClient _bot;
        private readonly AppDbContext _db;
        private static readonly ILogger Log = Serilog.Log.ForContext<Telegramm>();
        

        private Telegramm(string botToken)
        {
            _bot = new TelegramBotClient(botToken);
            _db = new AppDbContext();
        }
        private static Telegramm _singlet;
        public static Telegramm CreateSingleton(string token)
        {
            if (_singlet == null)
            {
                _singlet = new Telegramm(token);
                Log.Debug("_singlet создан!!");
            }
            return _singlet;
        }
        public static async Task SendPhoto(long? chatId, Stream stream, string text)
        {
            Log.Debug("Отправка фото пользователю {ChatId}...", chatId);

            await _singlet._bot.SendPhoto(chatId, stream, caption: text);

            Log.Debug("Фото отправлено!");
        }

        //Запуск слушателя
        public async Task StartAsync()
        {
            Log.Information("Запуск бота...");
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
            try
            {
                //#########################################################################################

                // if (update is { CallbackQuery: { } query }) // non-null CallbackQuery
                // {
                //     Log.Debug("Бот получил апдейт {Update.CallbackQuery}", update.ChosenInlineResult);

                //     // await bot.AnswerCallbackQuery(query.Id, $"You picked {query.Data}");
                //     // await bot.SendMessage(query.Message!.Chat, $"User {query.From} clicked on {query.Data}");
                //     // var callbackData = "calendar:prev:2024-12";
                //     // var parsedData = CalendarBuilder.ParseCalendarCallback(callbackData);
                //     // Log.Debug("Текущая дата: {ParsedData}", parsedData);
                    
                // }

                //###########################################################################################

                if (update.Type != UpdateType.Message || update.Message == null)
                    return;
                
                var message = update.Message;
                var chatId = (int)message.Chat.Id;

                Log.Debug("Обработка сообщения от пользователя {ChatId}", chatId);

                switch (message.Text)
                {
                    case "/start":
                        await HandleStartCommand(chatId, message);
                        break;

                    case "/photo":
                        await HandlePhotoCommand(chatId, message);
                        break;

                    case "/scheduledphoto":
                        await CreateScheduledPhotoCommand(DateTimeOffset.Now.AddMinutes(1), message, chatId);
                        break;

                    case "/subscribe":
                        await Subscribe(chatId, message);
                        break;
                    
                    case "/unsubscribe":
                        await Unsubscribe(chatId, message);
                        break;

                    default:
                        await bot.SendMessage(chatId, "Не понял команду");
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обработке соо");
            }
        }

        //Обработка команды /start
        private async Task HandleStartCommand(int chatId, Message message)
        {
            try
            {
                Log.Debug("Обработка /start");
                var username = message.Chat.Username ?? $"user_{message.Chat.Id}";
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);

                if (user == null)
                {
                    user = new User
                    {
                        Username = username,
                        FirstName = message.Chat.FirstName ?? "",
                        LastName = message.Chat.LastName ?? "",
                        RegisteredAt = DateTimeOffset.Now,
                        TGUserId = chatId
                    };

                    _db.Users.Add(user);
                    await _db.SaveChangesAsync();

                    await _bot.SendMessage(chatId,
                        "👋 Привет! Ты зарегистрирован. Напиши /photo чтобы сделать тестовое фото.");
                    
                    Log.Information("Добавлен пользователь {Username}", username);
                }
                else
                {
                    await _bot.SendMessage(chatId, "Ты уже зарегистрирован 😉");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обработке /start");
            }
        }

        //Обработка команды /photo
        private async Task HandlePhotoCommand(int chatId, Message message)
        {
            try
            {
                Log.Debug("Обработка /photo");
                var username = message.Chat.Username ?? $"user_{message.Chat.Id}";
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (user == null)
                {
                    await _bot.SendMessage(chatId, "⚠️ Сначала напиши /start, чтобы зарегистрироваться.");
                    return;
                }

                await CreateTask(TaskType.Photo, null, chatId, user.Id, null);
                Log.Debug("Создание задачи пользователем {User.Id}...", user.Id);

                await _bot.SendMessage(chatId, "📸 Задача на фото создана. Сейчас обработаю!");

            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обработке /photo");
            }
        }

        public async Task CreateScheduledPhotoCommand(DateTimeOffset scheduledTime, Message message, int chatId)
        {
            try
            {
                Log.Debug("Обработка /scheduledphoto");
                var username = message.Chat.Username ?? $"user_{message.Chat.Id}";
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (user == null)
                {
                    await _bot.SendMessage(chatId, "⚠️ Сначала напиши /start, чтобы зарегистрироваться.");
                    return;
                }

                // await calendarBuilder.SendCalendarMessageAsync(_bot, chatId, "Вот ваш календарь:", 2024, 12, CalendarViewType.Default);
                // var msg = await _bot.SendHtml(chatId, """
                //     <img src="https://telegrambots.github.io/book/docs/photo-ara.jpg">
                //     Do you like this photo?
                //     <keyboard>
                //     <button text="Yes" callback="ara-yes">
                //     <button text="No" callback="ara-no">
                //     </keyboard>
                //     """);

                await CreateTask(TaskType.Photo, null, chatId, null, scheduledTime);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обработке /scheduledphoto");
            }
        }

        //Отправка видео
        // public async Task SendVideo(string videoName, string botToken, int chatID)
        // {
        //     using var cts = new CancellationTokenSource();
        //     var bot = new TelegramBotClient(botToken, cancellationToken: cts.Token);

        //     await using Stream stream = File.OpenRead($"./{videoName}");
        //     await bot.SendVideo(chatID, stream);
        // }

        private async Task Subscribe(int chatId, Message message)
        {
            try
            {
                Log.Debug("Обработка /subscribe");
                var username = message.Chat.Username ?? $"user_{message.Chat.Id}";
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (user == null)
                {
                    await _bot.SendMessage(chatId, "⚠️ Сначала напиши /start, чтобы зарегистрироваться.");
                    return;
                }

                if (user.SunsetSubscribtion)
                {
                    await _bot.SendMessage(chatId, "Ты уже подписан:)");
                    return;
                }

                user.SunsetSubscribtion = true;
                await _db.SaveChangesAsync();
                await _bot.SendMessage(chatId, "Подписка на таймлапс оформлена!");

                Log.Debug("Пользователь {Username} подписался", username);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обработке /subscribe");
            }
        }

        private async Task Unsubscribe(int chatId, Message message)
        {
            try
            {
                Log.Debug("Обработка /unsubscribe");
                var username = message.Chat.Username ?? $"user_{message.Chat.Id}";
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (user == null)
                {
                    await _bot.SendMessage(chatId, "⚠️ Сначала напиши /start, чтобы зарегистрироваться.");
                    return;
                }

                if (!user.SunsetSubscribtion)
                {
                    await _bot.SendMessage(chatId, "Ты не подписан на таймлапс");
                    return;
                }

                user.SunsetSubscribtion = false;
                await _db.SaveChangesAsync();
                await _bot.SendMessage(chatId, "Подписка на таймлапс отменена:(");

                Log.Debug("Пользователь {Username} отписался", username);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обработке /unsubscribe");
            }
        }

        //Обработчик ошибок Telegram API
        private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken token)
        {
            Log.Error(exception.Message, "Ошибка в боте :(");
            return Task.CompletedTask;
        }

        private async Task CreateTask(TaskType type, string? parameters, long? chatId, int? userId, DateTimeOffset? scheduledTime)
        {
            var task = new TaskItem
            {
                Type = type,
                Status = TaskStatus.Created,
                Parameters = parameters,
                ChatId = chatId,
                UserId = userId,
                CreatedAt = DateTimeOffset.Now,
                ScheduledAt = scheduledTime
            };
            _db.Tasks.Add(task);
            await _db.SaveChangesAsync();

            Log.Debug("Добавлена задача типа {Type} пользователем {UserId}", type, userId);

            await Worker.NotifyNewTask();
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