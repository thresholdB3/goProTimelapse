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
            var settings = Settings.ReadSettings();

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
                .WriteTo.Console(outputTemplate: settings.Logger.outputTemplateConsole)
                .WriteTo.File(
                    path: settings.Logger.logPath,
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: 10_485_760, // 10 МБ
                    retainedFileCountLimit: 31,
                    outputTemplate: settings.Logger.outputTemplateFile)
                .CreateBootstrapLogger();

            try
            {
                Log.Information("Запуск приложения");

                using (var db = new AppDbContext()) //чтобы бд создавалась нормально
                {
                    db.Database.Migrate();
                }
                
                var telegramBot = Telegramm.CreateSingleton(settings.Telegramm.botToken);
                var worker = new Worker(settings);
                var sunsetPlanner = new SunsetPlanner();

                //var wlanWorker = new WlanWorker(settings.Network);
                //wlanWorker.Connect(settings.GoPro.CameraSSID, settings.GoPro.CameraPassword);

                await Task.Delay(5000);

                var cts = new CancellationTokenSource();

                Log.Information("Запускаем все задачи...");
                var botTask = telegramBot.StartAsync(cts.Token);
                var workerTask = worker.StartAsync(cts.Token);
                var sunsetPlannerTask = sunsetPlanner.StartAsync(cts.Token);

                Console.WriteLine("Нажми Enter для выхода...");
                Console.ReadLine();

                Log.Information("Останавливаем приложение...");
                cts.Cancel();

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
        }
    }
}
