using Microsoft.Extensions.Configuration;
using System.IO;

namespace GoProTimelapse
{
    public class Settings
    {
        public NetworkSettings Network { get; set; }
        public GoProSettings GoPro { get; set; }
        public TimelapsSettings Timelaps { get; set; } //??
        public BaseSettings Base { get; set; }
        public TelegramSettings Telegramm { get; set; }

        //метод для загрузки настроек из JSON
        public static Settings ReadSettings()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..\\..\\.."));
            var configuration = new ConfigurationBuilder()
                .SetBasePath(projectRoot)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var settings = new Settings();
            configuration.Bind(settings);
            return settings;
        }

    }
    public class TelegramSettings
    {
        public string botToken { get; set; }
        public string chatID { get; set; }
    }

    public class NetworkSettings
    {
        public string MainSSID { get; set; }
        public string MainPassword { get; set; }
    }

    public class GoProSettings
    {
        public string CameraSSID { get; set; }
        public string CameraPassword { get; set; }
        public GoProUrls Urls { get; set; }
        public GoProCommands Commands { get; set; } 
    }

    public class GoProUrls
    {
        public string BaseUrl { get; set; }
        public string MediaUrl { get; set; }
        public string FolderUrl { get; set; }
        public string MediaHtml { get; set; }
    }

    public class GoProCommands
    {
        public string SetMode { get; set; }
        public string Trigger { get; set; }
        public string Stop { get; set; }
    }

    public class TimelapsSettings
    {
        public int PhotoDelaySeconds { get; set; }
    }

    public class BaseSettings
    {
        public string DownloadFolder { get; set; }
    }
}
