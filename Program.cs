using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using System.Transactions;
using System.Text;
using Microsoft.Extensions.Configuration; 


class Program
{
    static async Task Main()
    {
        HttpClient client = new HttpClient();
        //здесь будет время начала и конца заката
        DateTime timeStart = DateTime.Now;
        DateTime timeStop = new DateTime(2025, 8, 6, 15, 30, 00);//год, месяц, день, часы, минуты, секунды

        string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..\\..\\.."));
        var configuration = new ConfigurationBuilder()
                            .SetBasePath(projectRoot) 
                            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true) 	.Build(); 

        int photoDelay = int.Parse(configuration["photoDelay"]);
        string baseUrl = configuration["url:baseUrl"];
        string mediaUrl = configuration["url:mediaUrl"];
        string folderUrl = configuration["url:folderUrl"];
        string mediaHtml = configuration["url:mediaHtml"];
        string downloadFolder = configuration["downloadFolder"];
        
        string ssid1 = configuration["wifi:main:ssid"]; //имя основного wifi
        string password1 = configuration["wifi:main:password"]; //его пароль
        string camera_ssid = configuration["wifi:camera:ssid"];
        string camera_password = configuration["wifi:camera:password"];


        ConnectWiFi(camera_ssid, camera_password);
        await Task.Delay(3000); //3 секунды на подключение

        //смена режима на фото; 0 - видео, 1 - фото, 2 - таймлапс
        await client.GetAsync(baseUrl + "/gp/gpControl/command/mode?p=1");

        if (!Directory.Exists(downloadFolder)) Directory.CreateDirectory(downloadFolder);

        await Task.Delay(3000); //3 секунды на переключение режима

        int photoCount = getPhotoCount();

        for (int i = 0; i < photoCount; i++)
        {
            await takePhoto();
            await downloadPhoto(i.ToString() + ".jpg");
            await Task.Delay(photoDelay - 11);//в коде уже есть задержка на 11 секунд
        }

        ConnectWiFi(ssid1, password1);

        createTimelapse();


        async Task takePhoto()
        {
            await client.GetAsync(baseUrl + "/gp/gpControl/command/shutter?p=1");
            await Task.Delay(5000); //5 секунд на фото и сохранение
        }

        async Task downloadPhoto(string fileName)
        {
            //получение последнего фото

            //поиск файлов
            var folderHtml = await client.GetStringAsync(folderUrl);
            var fileMatches = Regex.Matches(folderHtml, mediaHtml + @"([^""]+\.JPG)""");//ищет файлы типа GOPR1234.JPG

            var file = fileMatches //поиск последнего
                        .Select(m => $"{"100GOPRO/"}{m.Groups[1].Value}")
                        .OrderBy(name => name)
                        .Last();

            //загрузка файла
            var fileUrl = $"{mediaUrl}/{file}";
            var localPath = Path.Combine(downloadFolder, fileName);
            var data = await client.GetByteArrayAsync(fileUrl);
            await File.WriteAllBytesAsync(localPath, data);
            await Task.Delay(6000);//6 секунд на загрузку

            //удаление файла
            await client.GetAsync("http://10.5.5.9/gp/gpControl/command/storage/delete/last");
        }
        void ExecuteNetshCommand(string arguments)
        {
            //создание процесса
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // Console.WriteLine(output);
        }
        string GenerateXml(string ssid, string password)
        {
            return $@"<?xml version=""1.0""?>
        <WLANProfile xmlns=""http://www.microsoft.com/networking/WLAN/profile/v1"">
        <name>{ssid}</name>
        <SSIDConfig>
            <SSID>
            <name>{ssid}</name>
            </SSID>
        </SSIDConfig>
        <connectionType>ESS</connectionType>
        <connectionMode>auto</connectionMode>
        <MSM>
            <security>
            <authEncryption>
                <authentication>WPA2PSK</authentication>
                <encryption>AES</encryption>
                <useOneX>false</useOneX>
            </authEncryption>
            <sharedKey>
                <keyType>passPhrase</keyType>
                <protected>false</protected>
                <keyMaterial>{password}</keyMaterial>
            </sharedKey>
            </security>
        </MSM>
        </WLANProfile>";
        }
        void ConnectWiFi(string ssid, string password)
        {
            //создание и сохранение xml
            string xmlProfile = GenerateXml(ssid, password);
            string tempPath = Path.Combine(Path.GetTempPath(), $"{ssid}_profile.xml");

            File.WriteAllText(tempPath, xmlProfile);

            ExecuteNetshCommand($"wlan add profile filename=\"{tempPath}\"");
            ExecuteNetshCommand($"wlan connect name=\"{ssid}\" ssid=\"{ssid}\"");

            File.Delete(tempPath);
        }
        int getPhotoCount()
        {
            TimeSpan shootingTime = timeStop - timeStart;
            Console.WriteLine($"Время съёмки: {shootingTime}");
            int secondsShootingTime = (int)shootingTime.TotalSeconds;

            //количество фото, которые нужно сделать
            return secondsShootingTime / photoDelay;
        }
        void createTimelapse(int outputFps = 25)
        {
            //получение путей
            string photosDirectory = Path.Combine(projectRoot, "GoProPhotos");
            string ffmpegPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");

            var imageFiles = Directory.GetFiles(photosDirectory, "*.jpg");

            //сортировка фото и создание списка
            var sortedImageFiles = imageFiles
                .Select(f => new
                {
                    Path = f,
                    Number = int.TryParse(Path.GetFileNameWithoutExtension(f), out int num) ? num : -1
                })
                .Where(x => x.Number != -1)
                .OrderBy(x => x.Number)
                .Select(x => x.Path)
                .ToList();

            string inputListPath = Path.Combine(AppContext.BaseDirectory, "input.txt");
            File.WriteAllLines(inputListPath, sortedImageFiles.Select(f => $"file '{f.Replace("'", @"'\''")}'"));
            
            //сложные аргументы для запуска ffmpeg
            string arguments = $"-f concat -safe 0 -i \"{inputListPath}\" -c:v libx264 -r {outputFps} -pix_fmt yuv420p \"{Path.Combine(projectRoot, DateTime.Now.ToString("ssmmhh.ddMMyyyy"))}.mp4\"";

            //сам запуск
            using (var process = new Process())
            {
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.StartInfo.FileName = ffmpegPath;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                //обработчики для чтения вывода
                process.OutputDataReceived += (sender, e) => outputBuilder.AppendLine(e.Data);
                process.ErrorDataReceived += (sender, e) => errorBuilder.AppendLine(e.Data);

                process.Start();
                //чтение вывода и ошибок, без этого всё зависает
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();

            }

            File.Delete(inputListPath);
        }
    }
}
