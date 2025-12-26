using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

using Telegram.Bot;
using Serilog;
using Serilog.Events;
using System.Security.Cryptography.X509Certificates;

namespace GoProTimelapse
{
    public abstract class TaskProcessor<TResult>
    {
        public readonly GoProCameraFake _camera;
        private readonly Settings _settings; //??
        protected ILogger Log => Serilog.Log.ForContext(GetType());
        public TaskProcessor()
        {
            _camera = new GoProCameraFake();
            // _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }
        public abstract Task<TResult> Execute(string? Parameters = null);

    }
    public readonly struct Unit
    {
        public static readonly Unit Value = new();
    }
    public class ProcessPhoto : TaskProcessor<Unit>
    {
        public override async Task<Unit> Execute(string? Parameters = null)
        {
            await _camera.SetMode(GoProCameraFake.CameraStatus.Photo);
            await new TakePhoto().Execute();
            await new SendMedia().Execute(Parameters);
            return Unit.Value;
        }
    }
    public class ProcessTimelapse : TaskProcessor<Unit>
    {
        public override async Task<Unit> Execute(string? Parameters = null)
        {

            var template = new
            {
                TimelapseDelay = "",
                Users = new long[0]
            };
            var data = JsonConvert.DeserializeAnonymousType(Parameters, template);
            int timelapseDelay = Convert.ToInt32(data.TimelapseDelay);

            Log.Debug("Время съёмки в милисекундах: {TimelapseDelay}", timelapseDelay); 

            await _camera.SetMode(GoProCameraFake.CameraStatus.Timelapse);
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
                string parameters = JsonConvert.SerializeObject(parametersJson);
                await new SendMedia().Execute(parameters);
            }
            return Unit.Value;
        }
    }
    public class TakePhoto : TaskProcessor<Unit>
    {
        public override async Task<Unit> Execute(string? Parameters = null)
        {
            Log.Debug("Фото сделано👍👍👍");
            return Unit.Value;
        }
    }
    public class DownloadLastMedia : TaskProcessor<Stream>
                                    //теперь наследует, но много текста
                                    //лол
    {
        public override async Task<Stream> Execute(string? Parameters = null)
        {
            var stream = await _camera.DownloadLastMedia();
            Log.Debug("Медиа скачано");
            return stream;
        }
    }
    public class SendMedia : TaskProcessor<Unit>
    {
        public override async Task<Unit> Execute(string? Parameters = null)
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

            return Unit.Value;
        }
    }
}
