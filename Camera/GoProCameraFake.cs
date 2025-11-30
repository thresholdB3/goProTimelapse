using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using Serilog.Events;


namespace GoProTimelapse
{
    public class GoProCameraFake : ICamera
    {
        private readonly Settings _settings;
        private readonly HttpClient _httpClient;
        public bool isBusy => false;
        private static readonly ILogger Log = Serilog.Log.ForContext<GoProCameraFake>();

        public GoProCameraFake(Settings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _httpClient = new HttpClient();
        }

        public async Task DownloadLastPhotoAsync(string fileName)
        {
            string downloadFolder = _settings.Base.DownloadFolder;


            //уже есть пачка фото с прошлого таймлапса, пока не надо

            await Task.Delay(1000);//6 секунд на загрузку
        }
        // public async Task SetPhotoMode()
        // {
        //     Console.WriteLine("переключение режима...");
        //     await Task.Delay(1000); //3 секунды на переключение режима
        // }
        public async Task TakePhoto()
        {
            Console.WriteLine("фото");
            await Task.Delay(1000); //5 секунд на фото и сохранение
            // переключение режима будет здесь же
        }

        public async Task StartTimeLapse()
        {
            Log.Information("Камера начинает снимать таймлапс...");
        }
        public async Task StopTimeLapse()
        {
            Log.Information("Камера закончила таймлапс:)");
        }
        public async Task<byte[]> DownloadLastPhoto()
        {
            byte[] placeholder = await File.ReadAllBytesAsync("тут путь указать к фото");
            return placeholder;
        }
        public async Task<byte[]> GetLastVideo()
        {
            byte[] placeholder = await File.ReadAllBytesAsync("тут путь указать к видео");
            return placeholder;
        }
    }
}
