using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace GoProTimelapse
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var settings = Settings.ReadSettings();
            var telegramBot = new Telegramm(settings.Telegramm.botToken);
            var worker = new Worker(settings.Telegramm.botToken, settings);

            //Токен отмены, чтобы можно было закрыть оба потока
            var cts = new CancellationTokenSource();

            var botTask = telegramBot.StartAsync();
            var workerTask = worker.StartAsync(cts.Token);


            Console.WriteLine("Нажми Enter для выхода...");
            Console.ReadLine();

            //Отменяем воркер
            cts.Cancel();

            //Ждём завершения обоих потоков
            await Task.WhenAll(botTask, workerTask);



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
