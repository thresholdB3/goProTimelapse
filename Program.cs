using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;

class Program
{
    static async Task Main()
    {
        HttpClient client = new HttpClient();
        string baseUrl = "http://10.5.5.9";
        string mediaUrl = "http://10.5.5.9:8080/videos/DCIM";
        string folderUrl = "http://10.5.5.9:8080/videos/DCIM/100GOPRO/";
        string downloadFolder = "GoProPhotos";
        int photoCount = 5;

        //смена режима на фото; 0 - видео, 1 - фото, 2 - таймлапс
        await client.GetAsync(baseUrl + "/gp/gpControl/command/mode?p=1");

        if (!Directory.Exists(downloadFolder)) Directory.CreateDirectory(downloadFolder);

        await Task.Delay(3000); //3 секунды на переключение режима

        await takePhoto();
        await downloadPhoto();

        //команды на отключение от камеры я не нашла, так что просто выключаем ¯\_(ツ)_/¯
        await client.GetAsync(baseUrl + "/gp/gpControl/command/system/sleep");

        async Task takePhoto()
        {
            for (int i = 0; i < photoCount; i++)
            {
                await client.GetAsync(baseUrl + "/gp/gpControl/command/shutter?p=1");
                await Task.Delay(3000); //3 секунды на фото
            }

            await Task.Delay(3000); //3 секунды на сохранение
        }

        async Task downloadPhoto()
        {
            //получение последних 5 фото

            //поиск файлов
            var folderHtml = await client.GetStringAsync(folderUrl);
            var fileMatches = Regex.Matches(folderHtml, @"href=""/videos/DCIM/100GOPRO/" + @"([^""]+\.JPG)""");//ищет файлы типа GOPR1234.JPG

            var files = fileMatches
                        .Select(m => $"{"100GOPRO/"}{m.Groups[1].Value}")
                        .OrderBy(name => name)
                        .TakeLast(photoCount)
                        .ToArray();

            foreach (var file in files)
            {
                var fileUrl = $"{mediaUrl}/{file}";
                var fileName = Path.GetFileName(file);
                var localPath = Path.Combine(downloadFolder, fileName);
                var data = await client.GetByteArrayAsync(fileUrl);
                await File.WriteAllBytesAsync(localPath, data);
            }
        }
    }
}
