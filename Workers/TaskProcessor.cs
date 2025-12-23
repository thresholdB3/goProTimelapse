using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Telegram.Bot;
using Serilog;
using Serilog.Events;
using System.Security.Cryptography.X509Certificates;

namespace GoProTimelapse
{
    public abstract class TaskProcessor
    {
        public readonly GoProCameraFake _camera;
        private readonly Settings _settings; //??
        protected ILogger Log => Serilog.Log.ForContext(GetType());
        public TaskProcessor()
        {
            _camera = new GoProCameraFake();
            // _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }
        public abstract Task Execute(string? Parameters = null); //Parameters будет json

    }

    public class ProcessPhoto : TaskProcessor
    {
        public override async Task Execute(string? Parameters = null)
        {
            await new TakePhoto().Execute();
            await new DownloadLastMedia().Execute();
            await new SendMedia().Execute();
        }
    }
    public class ProcessTimelapse : TaskProcessor
    {
        public override async Task Execute(string? Parameters = null)
        {

            var template = new
            {
                TimelapseDelay = "",
                Users = new long[0]
            };
            var data = JsonConvert.DeserializeAnonymousType(Parameters, template);
            int timelapseDelay = Convert.ToInt32(data.TimelapseDelay);

            Log.Debug("Время съёмки в милисекундах: {TimelapseDelay}", timelapseDelay); 

            await _camera.StartTimeLapse();

            // await Task.Delay(timelapseDelay);

            await _camera.StopTimeLapse();

            //тут что то про сохранение файла
            // await new DownloadLastMedia().Execute();
            // var stream = await _camera.DownloadLastMedia();

            foreach (var userId in data.Users)
            {
                await new SendMedia().Execute(userId.ToString());
            }
        }
    }
    public class TakePhoto : TaskProcessor
    {
        public override async Task Execute(string? Parameters = null)
        {
            Log.Debug("Фото сделано");
        }
    }
    public class DownloadLastMedia : TaskProcessor
    {
        public override async Task Execute(string? Parameters = null) 
        {
            var stream = await _camera.DownloadLastMedia();
            Log.Debug("Медиа скачано");
        }
    }
    public class SendMedia : TaskProcessor
    {
        public override async Task Execute(string? Parameters = null)
        {
            await using var stream = File.OpenRead(@"GoProPhotos\1.jpg"); //потом переделаю, пока не знаю как, думать надо:(
            Log.Debug("Отправка фото пользователю {parameters}", Parameters);
            await Telegramm.SendPhoto(long.Parse(Parameters), stream, "📸 Вот твоё медиа!");
            Log.Debug("Медиа отправлено");
        }
    }
}
