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
    public class GoProCamera : ICamera
    {
        private readonly Settings _settings;
        private readonly HttpClient _httpClient;
        public bool isBusy { get; set; }
        private static readonly ILogger Log = Serilog.Log.ForContext<GoProCameraFake>();

        private static GoProCamera _singlet;
        private GoProCamera(Settings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _httpClient = new HttpClient();
        }
        public static GoProCamera CreateSingleton()
        {
            if (_singlet == null)
            {
                var settings = Settings.ReadSettings();
                _singlet = new GoProCamera(settings);
                Log.Debug("Камера создана <(-*-)>");
            }
            return _singlet;
        }

        private async Task SetMode(CameraStatus mode)
        {
            Log.Debug("переключение режима на {mode}...", mode);

            string url = $"{_settings.GoPro.Urls.BaseUrl}/gp/gpControl/command/mode?p={(int)mode}";
            await _httpClient.GetAsync(url);

            await Task.Delay(3000); //3 секунды на переключение режима
        }
        public async Task TakePhoto()
        {
            Log.Debug("фото происходит...");
            await SetMode(CameraStatus.Photo);

            string url = _settings.GoPro.Urls.BaseUrl + "/gp/gpControl/command/shutter?p=1";
            Log.Debug("фото происходит по ссылке {ogo}", url);
            await _httpClient.GetAsync(url);

            await Task.Delay(5000); //5 секунд на фото и сохранение
        }
        public async Task<byte[]> DownloadLastMedia(string Type)
        {
            string folderUrl = _settings.GoPro.Urls.FolderUrl;
            string mediaUrl = _settings.GoPro.Urls.MediaUrl;
            string mediaHtml = _settings.GoPro.Urls.MediaHtml;
            string downloadFolder = _settings.Base.DownloadFolder;

            if (!Directory.Exists(downloadFolder))
                Directory.CreateDirectory(downloadFolder);

            var folderHtml = await _httpClient.GetStringAsync(folderUrl);
            MatchCollection fileMatches;//ищет файлы типа GOPR1234.JPG

            if (Type == ".jpg")
            {
                fileMatches = Regex.Matches(folderHtml, mediaHtml + @"([^""]+\.JPG)""");
            }
            else
            {
                fileMatches = Regex.Matches(folderHtml, mediaHtml + @"([^""]+\.MP4)""");
            }

            var lastFile = fileMatches //поиск последнего
                .Select(m => $"100GOPRO/{m.Groups[1].Value}")
                .OrderBy(name => name)
                .Last();

            var fileUrl = $"{mediaUrl}/{lastFile}";
            var media = await _httpClient.GetByteArrayAsync(fileUrl);
            await Storage.SaveFile(media, Type);

            return media;
        }
        public async Task MakeTimelapse(TimeSpan delay)
        {
            await SetMode(CameraStatus.Timelapse);
            isBusy = true;
            Log.Information("Камера начинает снимать таймлапс...");
            await _httpClient.GetAsync(_settings.GoPro.Commands.Trigger);

            await Task.Delay(delay);

            await _httpClient.GetAsync(_settings.GoPro.Commands.Stop);
            await Task.Delay(5000);

            isBusy = false;
            Log.Information("Камера закончила таймлапс:)");
        }
    }
}
