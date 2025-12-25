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
        public bool isBusy { get; set; }
        private static readonly ILogger Log = Serilog.Log.ForContext<GoProCameraFake>();
        

        public GoProCameraFake()
        {

            _httpClient = new HttpClient();
        }


        public async Task SetMode(int mode)
        {
            Log.Debug("переключение режима на {mode}...", mode);
            //не помню какой режим за что, но 1 - это фото, остальные потом гляну
            await Task.Delay(1000); 
        }
        public async Task TakePhoto()
        {
            SetMode(1);
            await Task.Delay(1000);
        }

        public async Task StartTimeLapse()
        {
            isBusy = true;
            Log.Information("Камера начинает снимать таймлапс...");
        }
        public async Task StopTimeLapse()
        {
            isBusy = false;
            Log.Information("Камера закончила таймлапс:)");
        }
        public async Task<Stream> DownloadLastMedia()
        {
            Stream placeholder = File.OpenRead(@"GoProPhotos\1.jpg");
            return placeholder;
        }
        // public async Task<byte[]> GetLastVideo()
        // {
        //     byte[] placeholder = await File.ReadAllBytesAsync("тут путь указать к видео");
        //     return placeholder;
        // }
    }
}
