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
            // await new DownloadLastMedia().Execute();
            await new SendMedia().Execute(Parameters);
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

            foreach (var userId in data.Users)
            {
                var parametersJson = new 
                {
                    user = userId,
                    message = "соо ещё не придумала(("
                };
                string parameters1 = JsonConvert.SerializeObject(parametersJson);
                // var parameters1 = System.Text.Json.JsonSerializer.Serialize(parametersJson);
                await new SendMedia().Execute(parameters1);
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
    public class DownloadLastMedia  //пусть прост не наследует
                                    //потом может что покруче придумаю
    {
        public readonly GoProCameraFake _camera;
        public DownloadLastMedia()
        {
            _camera = new GoProCameraFake();
        }
        public async Task<Stream> Execute()
        {
            var stream = await _camera.DownloadLastMedia();
            Log.Debug("Медиа скачано");
            return stream;
        }
    }
    public class SendMedia : TaskProcessor
    {
        public override async Task Execute(string? Parameters = null)
        {
            var template = new
            {
                user = 0L,
                message = ""
            };
            var data = JsonConvert.DeserializeAnonymousType(Parameters, template);
            var userId = Convert.ToInt64(data.user);
            var message = Convert.ToString(data.message);
            var stream = await new DownloadLastMedia().Execute();
            Log.Debug("Отправка фото пользователю {userId}", userId);
            await Telegramm.SendPhoto(userId, stream, message);
            Log.Debug("Медиа отправлено");
        }
    }
}
