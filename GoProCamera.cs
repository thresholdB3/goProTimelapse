using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;

namespace GoProTimelapse
{
    public class GoProCamera
    {
        private readonly Settings _settings;
        private readonly HttpClient _httpClient;

        public GoProCamera(Settings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _httpClient = new HttpClient();
        }

        public async Task DownloadLastPhotoAsync(string fileName)
        {
            string folderUrl = _settings.GoPro.Urls.FolderUrl;
            string mediaUrl = _settings.GoPro.Urls.MediaUrl;
            string mediaHtml = _settings.GoPro.Urls.MediaHtml;
            string downloadFolder = _settings.Base.DownloadFolder;

            if (!Directory.Exists(downloadFolder))
                Directory.CreateDirectory(downloadFolder);

            var folderHtml = await _httpClient.GetStringAsync(folderUrl);
            var fileMatches = Regex.Matches(folderHtml, mediaHtml + @"([^""]+\.JPG)""");//ищет файлы типа GOPR1234.JPG

            var lastFile = fileMatches //поиск последнего
                .Select(m => $"100GOPRO/{m.Groups[1].Value}")
                .OrderBy(name => name)
                .Last();

            var fileUrl = $"{mediaUrl}/{lastFile}";
            var localPath = Path.Combine(downloadFolder, fileName);

            var data = await _httpClient.GetByteArrayAsync(fileUrl);
            await File.WriteAllBytesAsync(localPath, data);
            await Task.Delay(6000);//6 секунд на загрузку

            await _httpClient.GetAsync("http://10.5.5.9/gp/gpControl/command/storage/delete/last");
        }
        public async Task SetPhotoModeAsync()
        {
            string url = _settings.GoPro.Urls.BaseUrl + "/gp/gpControl/command/mode?p=1";
            await _httpClient.GetAsync(url);
            await Task.Delay(3000); //3 секунды на переключение режима
        }
        public async Task TakePhotoAsync()
        {
            string url = _settings.GoPro.Urls.BaseUrl + "/gp/gpControl/command/shutter?p=1";
            await _httpClient.GetAsync(url);
            await Task.Delay(5000); //5 секунд на фото и сохранение
        }
    }
}
