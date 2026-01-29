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
    

        //todo: 
        private GoProCameraFake()
        {

            _httpClient = new HttpClient();
        }
        private static GoProCameraFake _singlet;
        public static GoProCameraFake CreateSingleton()
        {
            if (_singlet == null)
            {
                _singlet = new GoProCameraFake();
                Log.Debug("Камера создана <(-*-)>");
            }
            return _singlet;
        }
        private async Task SetMode(CameraStatus mode)//будет в самой камере использоваться в методах и private
        {
            Log.Debug("переключение режима на {mode}...", mode);
            //не помню какой режим за что, но 1 - это фото, остальные потом гляну
            await Task.Delay(1000); 
        }
        public async Task TakePhoto()
        {
            await SetMode(CameraStatus.Photo);
            await Task.Delay(1000);
        }

        public async Task<byte[]> DownloadLastMedia(MediaType Type) //будет аргумет видео или фото
        {
            byte[] placeholder;
            if (Type == MediaType.Photo)
            {
                placeholder = File.ReadAllBytes(@"GoProPhotos\1.jpg");
                await Storage.SaveFile(placeholder, ".jpg"); //todo: сделать константы для расширений
            }
            else
            {
                placeholder = File.ReadAllBytes(@"GoProPhotos\ogo.mp4");
                await Storage.SaveFile(placeholder, ".mp4");
            }
            return placeholder;
        }

        public async Task MakeTimelapse(TimeSpan delay)
        {
            await SetMode(CameraStatus.Timelapse);
            isBusy = true;
            Log.Information("Камера начинает снимать таймлапс...");

            await Task.Delay(delay);

            isBusy = false;
            Log.Information("Камера закончила таймлапс:)");
        }

        //public startvideo
        //public stopvideo
    }
}
