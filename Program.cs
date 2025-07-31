using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;

class Program
{
    static async Task Main()
    {
        HttpClient client = new HttpClient();
        string baseUrl = "http://10.5.5.9";
        string mediaUrl = "http://10.5.5.9:8080/videos/DCIM";
        string folderUrl = "http://10.5.5.9:8080/videos/DCIM/100GOPRO/";
        string mediaHtml = @"href=""/videos/DCIM/100GOPRO/";
        string downloadFolder = "GoProPhotos";
        int photoCount = 5;
        string ssid1 = "vivo Y22"; //имя основного wifi
        string password1 = "levynosok0"; //его пароль
        string camera_ssid = "GP53119127";
        string camera_password = "p=n-6VT-6Kn";

        ConnectWiFi(camera_ssid, camera_password);
        await Task.Delay(3000); //3 секунды на подключение

        //смена режима на фото; 0 - видео, 1 - фото, 2 - таймлапс
        await client.GetAsync(baseUrl + "/gp/gpControl/command/mode?p=1");

        if (!Directory.Exists(downloadFolder)) Directory.CreateDirectory(downloadFolder);

        await Task.Delay(3000); //3 секунды на переключение режима

        for (int i = 0; i < photoCount; i++)
        {
            await takePhoto();
            await downloadPhoto();
        }

        ConnectWiFi(ssid1, password1);

        async Task takePhoto()
        {
            await client.GetAsync(baseUrl + "/gp/gpControl/command/shutter?p=1");
            await Task.Delay(3000); //3 секунды на фото
            await Task.Delay(3000); //3 секунды на сохранение
        }

        async Task downloadPhoto()
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
            var fileName = Path.GetFileName(file);
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
    }
}
