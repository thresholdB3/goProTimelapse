using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;

namespace GoProTimelapse
{
    public class GoProCameraFake
    {
        private readonly Settings _settings;
        private readonly HttpClient _httpClient;

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
        public async Task SetPhotoModeAsync()
        {
            Console.WriteLine("переключение режима...");
            await Task.Delay(1000); //3 секунды на переключение режима
        }
        public async Task TakePhotoAsync()
        {
            Console.WriteLine("фото");
            await Task.Delay(1000); //5 секунд на фото и сохранение
        }
    }
}
