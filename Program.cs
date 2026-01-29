using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

namespace GoProTimelapse
{
    class Program
    {
        static async Task Main(string[] args)
        {
            

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()                   
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            #if DEBUG
                .MinimumLevel.Debug()                            
            #endif
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [Thread:{ThreadId}] {Message:lj} <{SourceContext}>{NewLine}{Exception}")
                    //потом вынесу в appsettings
                .WriteTo.File(
                    path: "Logs/log-.txt",
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: 10_485_760, // 10 МБ
                    retainedFileCountLimit: 31,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [Thread:{ThreadId}] {MachineName} {SourceContext} | {Message:lj}{NewLine}{Exception}")
                    //это тоже
                .CreateBootstrapLogger();
            //логи пока кривовато, потом нормально доделаю

            try
            {
                Log.Information("Запуск приложения");

                using (var db = new AppDbContext()) //чтобы бд создавалась нормально
                {
                    db.Database.Migrate();
                }

                var settings = Settings.ReadSettings();
                var telegramBot = Telegramm.CreateSingleton(settings.Telegramm.botToken);
                var worker = new Worker(settings);
                var sunsetPlanner = new SunsetPlanner();

                var cts = new CancellationTokenSource();

                Log.Information("Запускаем все задачи...");
                var botTask = telegramBot.StartAsync(cts.Token);
                var workerTask = worker.StartAsync(cts.Token);
                var sunsetPlannerTask = sunsetPlanner.StartAsync(cts.Token);

                Console.WriteLine("Нажми Enter для выхода...");
                Console.ReadLine();

                Log.Information("Останавливаем приложение...");
                cts.Cancel();

                //await Task.WhenAll(botTask, workerTask);

                Log.Information("Все задачи завершены. Выход.");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Приложение упало с необработанным исключением");
                throw;
            }
            finally
            {
                await Log.CloseAndFlushAsync(); 
            }

            



            // var settings = Settings.ReadSettings();
            // var wlanWorker = new WlanWorker(settings.Network);
            // var camera = new GoProCameraFake(settings);
            // var telegramm = new Telegramm();

            // wlanWorker.Connect(settings.GoPro.CameraSSID, settings.GoPro.CameraPassword);
            // await Task.Delay(3000); //3 секунды на подключение
            // await camera.SetPhotoModeAsync();

            // if (!Directory.Exists(settings.Base.DownloadFolder))
            //     Directory.CreateDirectory(settings.Base.DownloadFolder);

            // DateTime timeStart = DateTime.Now;
            // DateTime timeStop = new DateTime(2025, 8, 30, 15, 30, 0);
            // int photoCount = settings.GetPhotoCount(timeStart, timeStop);
            // int photoCount = 756;


            // for (int i = 0; i < photoCount; i++)
            // {
            //     Console.WriteLine($"Съёмка фото {i + 1}/{photoCount}...");
            //     await camera.TakePhotoAsync();
            //     await camera.DownloadLastPhotoAsync(i.ToString() + ".jpg");
            //     photoFiles.Add(Path.Combine(settings.Base.DownloadFolder, i.ToString() + ".jpg"));
            //     await Task.Delay(settings.Timelaps.PhotoDelaySeconds - 11);//в коде уже есть задержка на 11 секунд
            //     //на самом деле не 11, надо тыкать и исправлять
            // }

            // wlanWorker.Connect(settings.Network.MainSSID, settings.Network.MainPassword);

            // await FFMpegWorker.CreateVideoFromPhotos(settings.Base.DownloadFolder);

            // await telegramm.SendVideo(outputFileName, settings.Telegramm.botToken, int.Parse(settings.Telegramm.chatID))

        }
    }
}
