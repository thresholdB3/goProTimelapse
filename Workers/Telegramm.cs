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
using Telegram.Bot.Types.ReplyMarkups;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;



namespace GoProTimelapse
{
    public class Telegramm
    {
        private readonly TelegramBotClient _bot;
        private readonly AppDbContext _db;
        private static readonly ILogger Log = Serilog.Log.ForContext<Telegramm>();
        static readonly ConcurrentDictionary<long, DateTimeOffset> DraftDates = new(); //для начала так, потом переделаю
        

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
                if (update.Type == UpdateType.Message && update.Message != null)
                {
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
                if (update.Type == UpdateType.CallbackQuery)
                {
                    var data = update.CallbackQuery.Data;
                    var chatId = update.CallbackQuery.Message.Chat.Id;
                    var messageId = update.CallbackQuery.Message.Id;
                    await HandleCallbackQuery(chatId, data, messageId); //пока предположим что такое есть только у план фото
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обработке соо");
            }
        }

        private async Task HandleCallbackQuery(long chatId, string data, int messageId)
        {
            Log.Debug("Обработка апдейта...");
            if (data[0] == 'D')
            {
                var scheduledTime = new DateTimeOffset(
                DateTime.Today.AddDays(data[1] - '0'),
                TimeSpan.FromHours(5));
                Log.Debug("Добавляем {s} дней...", data[1]);

                DraftDates[chatId] = scheduledTime;

                var keyboard = await UpdateInline();
                await _bot.EditMessageText(chatId, messageId, "когда фото??1", replyMarkup: keyboard);

                return;
            }
            if (data[0] == 'T')
            {
                var scheduledTime = DraftDates[chatId].AddHours(int.Parse(data.Substring(1)));
                if (scheduledTime <= DateTimeOffset.Now)
                {
                    await _bot.SendMessage(chatId, "не");
                    return;
                }
                await CreateTask(TaskType.Photo, null, chatId, null, scheduledTime);
                Log.Debug("Запланировано на {ScheduledTime}", scheduledTime);
            }
            if (data[0] == 'S')
            {
                InlineKeyboardMarkup? keyboard = await UpdateInline(int.Parse(data.Substring(1)));
                if (keyboard == null)
                {
                    return;
                }
                await _bot.EditMessageText(chatId, messageId, "когда фото??1", replyMarkup: keyboard);
                return;
            }

        }

        private async Task<InlineKeyboardMarkup> UpdateInline(int Page = 0)
        {
            if ((Page < 0) || (Page > 21))
            {
                return null;
            }
            InlineKeyboardMarkup keyboard = new(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData($"{0 + Page}", $"T{0 + Page}"),
                    InlineKeyboardButton.WithCallbackData($"{1 + Page}", $"T{1 + Page}"),
                    InlineKeyboardButton.WithCallbackData($"{2 + Page}", $"T{2 + Page}"),
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("<-", $"S{Page - 3}"),
                    InlineKeyboardButton.WithCallbackData("->", $"S{Page + 3}"),
                },
            });
            Log.Debug("Страница {s}, кнопки {d} и {g}", Page, Page - 3, Page + 3);
            return keyboard;
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

                InlineKeyboardMarkup keyboard = new(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("сегодня", "D0"),
                        InlineKeyboardButton.WithCallbackData("завтра", "D1"),
                        InlineKeyboardButton.WithCallbackData("послезавтра", "D2"),
                    },
                });
                // InlineKeyboardMarkup keyboard1 = new(new[]
                // {
                //     new[]
                //     {
                //         InlineKeyboardButton.WithCallbackData("сегодня1", "0"),
                //         InlineKeyboardButton.WithCallbackData("завтра1", "1"),
                //         InlineKeyboardButton.WithCallbackData("послезавтра1", "2"),
                //     },
                // });
                var msg = await _bot.SendMessage(chatId, "когда фото??", replyMarkup: keyboard);

                // var msg = await _bot.SendHtml(chatId, """ 
                //     На какое время??
                //     <keyboard>
                //     <button text="9:00" callback="9">
                //     <button text="10:00" callback="10">
                //     <button text="11:00" callback="11">
                //     <button text="12:00" callback="12">
                //     <button text="13:00" callback="13">
                //     <button text="14:00" callback="14">
                //     <row>
                //     <button text="15:00" callback="15">
                //     <button text="16:00" callback="16">
                //     <button text="17:00" callback="17">
                //     <button text="18:00" callback="18">
                //     <button text="19:00" callback="19">
                //     <button text="20:00" callback="20">
                //     </keyboard>
                //     """); //потом напишу что нибудь чтобы само генерилось
                          //и выглядело круче
                    // await _bot.EditMessageText(chatId, msg.MessageId, "когда фото??1", replyMarkup: keyboard1);

                // await CreateTask(TaskType.Photo, null, chatId, null, scheduledTime);
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