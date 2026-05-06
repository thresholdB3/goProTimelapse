using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using Telegram.Bot;
using Telegram.Bot.Extensions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.CalendarKit;
using static System.Net.Mime.MediaTypeNames;



namespace GoProTimelapse
{
    public class Telegramm
    {
        private readonly TelegramBotClient _bot;
        private readonly AppDbContext _db;
        private static readonly ILogger Log = Serilog.Log.ForContext<Telegramm>();
        static readonly ConcurrentDictionary<long, DateTimeOffset> DraftDates = new(); //для начала так, потом переделаю
        private readonly GoProCamera _camera;



        //private Telegramm(string botToken)
        //{
        //    var handler = new HttpClientHandler();
        //    var httpClient = new HttpClient(handler)
        //    {
        //        Timeout = TimeSpan.FromMinutes(30)
        //    };
        //    var options = new TelegramBotClientOptions(
        //        token: botToken
        //       // baseUrl: "http://127.0.0.1:8081"
        //    );
        //    _bot = new TelegramBotClient(options, httpClient);
        //    _db = new AppDbContext();
        //    _camera = GoProCameraFake.CreateSingleton();
        //}
        private Telegramm(string botToken)
        {
            _bot = new TelegramBotClient(botToken);
            _db = new AppDbContext();
            _camera = GoProCamera.CreateSingleton();
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
        public static async Task SendMedia(long? chatId, Stream stream, string text, MediaType type)
        {
            Log.Information("Отправка фото пользователю {ChatId}...", chatId);

            if (type == MediaType.Photo)
            {
                await _singlet._bot.SendPhoto(chatId, stream, caption: text);
            }
            else
            {
                await _singlet._bot.SendVideo(chatId, stream, caption: text);
            }
        }

        //Запуск слушателя
        public async Task StartAsync(CancellationToken cts)
        {
            Log.Information("Запуск бота...");
            var me = await _bot.GetMe();
            

            _bot.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                cancellationToken: cts
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

                    switch (message.Text) //todo: getLastVideo
                    {
                        case "/start":
                            await HandleStartCommand(chatId, message);
                            break;

                        case "/photo":
                            await HandlePhotoCommand(chatId, message);
                            break;

                        case "/schedulephoto":
                            await CreateScheduledPhotoCommand(message, chatId);
                            break;
                        
                        case "/scheduletimelapse":
                            await CreateScheduledTimelapseCommand(message, chatId);
                            break;

                        case "/subscribe":
                            await Subscribe(chatId, message);
                            break;
                        
                        case "/unsubscribe":
                            await Unsubscribe(chatId, message);
                            break;

                        case "/lastphoto":
                            await GetLastPhoto(chatId, message);
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
                    await HandleCallbackQuery(chatId, data, messageId);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обработке соо");
            }
        }

        private async Task HandleCallbackQuery(long chatId, string data, int messageId)//большая штука, надо поделить на несколько
                                                                                       //а может и не надо
                                                                                       //подумать надо
        {
            try
            {
                Log.Information("Обработка апдейта...");
                if (data[1] == 'D')
                {
                    var scheduledTime = new DateTimeOffset(
                    DateTime.Today.AddDays(data[2] - '0'),
                    TimeSpan.FromHours(5));

                    DraftDates[chatId] = scheduledTime;

                    var keyboard = await UpdateInline(data[0]);
                    await _bot.EditMessageText(chatId, messageId, "__〆(．．) На какое время запланировать?", replyMarkup: keyboard);

                    return;
                }
                if (data[1] == 'T')
                {
                    var scheduledTime = DraftDates[chatId].AddHours(int.Parse(data.Substring(2)));
                    if (scheduledTime <= DateTimeOffset.Now)
                    {
                        await _bot.SendMessage(chatId, "(￣ヘ￣) Нельзя запланировать на прошлое");
                        return;
                    }

                    bool exist = await _db.Tasks
                        .AnyAsync(t => t.ScheduledAt == scheduledTime);
                    if (exist)
                    {
                        await _bot.SendMessage(chatId, "(*_ _)人 Камера занята, попробуй другое время");
                        return;
                    }
                    if (data[0] == 'P')
                    {
                        await CreateTask(TaskType.Photo, null, chatId, null, scheduledTime);
                        Log.Information("Фото Запланировано на {ScheduledTime}", scheduledTime);
                        await _bot.SendMessage(chatId, "Фото запланировано (*￣▽￣)b");
                        await _bot.DeleteMessage(chatId, messageId);
                        return;
                    }
                    if (data[0] == 'T')
                    {
                        await CreateTask(TaskType.Timelapse, TimeSpan.FromMinutes(30).ToString(), chatId, null, scheduledTime);
                        Log.Information("Таймлапс запланирован на {ScheduledTime}", scheduledTime);
                        await _bot.SendMessage(chatId, "Таймлапс запланирован (*￣▽￣)b");
                        await _bot.DeleteMessage(chatId, messageId);
                        return;
                    }
                }

                if (data[1] == 'S')
                {
                    InlineKeyboardMarkup? keyboard = await UpdateInline(data[0], Step: data[1], Page: int.Parse(data.Substring(2)));
                    if (keyboard == null)
                    {
                        return;
                    }
                    await _bot.EditMessageText(chatId, messageId, "__〆(．．) На какое время запланировать?", replyMarkup: keyboard);
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обработке апдейта :(");
            }

        }

        private async Task<InlineKeyboardMarkup> UpdateInline(char Type, char Step = 'T', int Page = 0)
        {
            if ((Page < 0) || (Page > 21))
            {
                return null;
            }
    
            InlineKeyboardMarkup keyboard = new(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData($"{0 + Page}:00", $"{Type}T{0  + Page}"),
                    InlineKeyboardButton.WithCallbackData($"{1 + Page}:00", $"{Type}T{1  + Page}"),
                    InlineKeyboardButton.WithCallbackData($"{2 + Page}:00", $"{Type}T{2  + Page}"),
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("<-", $"{Type}S{Page - 3}"),
                    InlineKeyboardButton.WithCallbackData("->", $"{Type}S{Page + 3}"),
                },
            });
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
                        "＼(⌒▽⌒) Привет! Ты зарегистрирован. Напиши /photo чтобы сделать тестовое фото.");
                    
                    Log.Information("Добавлен пользователь {Username}", username);
                }
                else
                {
                    await _bot.SendMessage(chatId, "☆(>ᴗ•) Ты уже зарегистрирован");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обработке /start");
            }
        }
        private async Task GetLastPhoto(int chatId, Message message)
        {
            try
            {
                Log.Debug("Обработка /lastphoto");
                var username = message.Chat.Username ?? $"user_{message.Chat.Id}";
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (user == null)
                {
                    await _bot.SendMessage(chatId, "|･д･)ﾉ Сначала напиши /start, чтобы зарегистрироваться.");
                    return;
                }
                var Photo = await Storage.GetLastFile(".jpg");
                await _bot.SendPhoto(chatId, Photo, caption: "(￣▽￣*)ゞ Последнее фото с камеры");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обработке /lastphoto");
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
                    await _bot.SendMessage(chatId, "|･д･)ﾉ Сначала напиши /start, чтобы зарегистрироваться.");
                    return;
                }

                if (_camera.isBusy == true)
                {
                    await _bot.SendMessage(chatId, "(￣ ￣|||) Камера занята, попробуй позже");
                    return;
                }

                await CreateTask(TaskType.Photo, null, chatId, user.Id, null);
                Log.Debug("Создание задачи пользователем {User.Id}...", user.Id);

                await _bot.SendMessage(chatId, "(*￣▽￣)b Задача на фото создана. Сейчас обработаю!");

            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обработке /photo");
            }
        }

        public async Task CreateScheduledPhotoCommand(Message message, int chatId)
        {
            try
            {
                Log.Debug("Обработка /scheduledphoto");
                var username = message.Chat.Username ?? $"user_{message.Chat.Id}";
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (user == null)
                {
                    await _bot.SendMessage(chatId, "|･д･)ﾉ Сначала напиши /start, чтобы зарегистрироваться.");
                    return;
                }

                InlineKeyboardMarkup keyboard = new(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Сегодня", "PD0"),
                        InlineKeyboardButton.WithCallbackData("Завтра", "PD1"),
                        InlineKeyboardButton.WithCallbackData("Послезавтра", "PD2"),
                    },
                });

                var msg = await _bot.SendMessage(chatId, "__〆(．．) На какой день запланировать?", replyMarkup: keyboard);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обработке /scheduledphoto");
            }
        }

        public async Task CreateScheduledTimelapseCommand(Message message, int chatId)
        {
            try
            {
                Log.Debug("Обработка /scheduledtimelapse");
                var username = message.Chat.Username ?? $"user_{message.Chat.Id}";
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (user == null)
                {
                    await _bot.SendMessage(chatId, "|･д･)ﾉ Сначала напиши /start, чтобы зарегистрироваться.");
                    return;
                }

                InlineKeyboardMarkup keyboard = new(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("сегодня", "TD0"),
                        InlineKeyboardButton.WithCallbackData("завтра", "TD1"),
                        InlineKeyboardButton.WithCallbackData("послезавтра", "TD2"),
                    },
                });

                var msg = await _bot.SendMessage(chatId, "__〆(．．) На какой день запланировать?", replyMarkup: keyboard);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обработке /scheduledtimelapse");
            }
        }
        private async Task Subscribe(int chatId, Message message)
        {
            try
            {
                Log.Debug("Обработка /subscribe");
                var username = message.Chat.Username ?? $"user_{message.Chat.Id}";
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (user == null)
                {
                    await _bot.SendMessage(chatId, "|･д･)ﾉ Сначала напиши /start, чтобы зарегистрироваться.");
                    return;
                }

                if (user.SunsetSubscribtion)
                {
                    await _bot.SendMessage(chatId, "(/ =ω=)/ Ты уже подписан");
                    return;
                }

                user.SunsetSubscribtion = true;
                await _db.SaveChangesAsync();
                await _bot.SendMessage(chatId, "(〜￣▽￣)〜 Подписка на таймлапс оформлена");

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
                    await _bot.SendMessage(chatId, "|･д･)ﾉ Сначала напиши /start, чтобы зарегистрироваться.");
                    return;
                }

                if (!user.SunsetSubscribtion)
                {
                    await _bot.SendMessage(chatId, "o(TヘTo) Ты не подписан на таймлапс");
                    return;
                }

                user.SunsetSubscribtion = false;
                await _db.SaveChangesAsync();
                await _bot.SendMessage(chatId, "( ╥ω╥ ) Подписка на таймлапс отменена");

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

            Log.Information("Добавлена задача типа {Type} пользователем {UserId}", type, userId);

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