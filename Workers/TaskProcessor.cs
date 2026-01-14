using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

using Telegram.Bot;
using Serilog;
using Serilog.Events;
using System.Security.Cryptography.X509Certificates;
using System.Diagnostics;

namespace GoProTimelapse
{
    public abstract class TaskProcessor<TArgs, TResult>
    {
        public readonly GoProCameraFake _camera;
        // private readonly Settings _settings; //??
        protected ILogger Log => Serilog.Log.ForContext(GetType());
        public TaskProcessor()
        {
            _camera = new GoProCameraFake();
            // _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }
        public abstract Task<TResult> Execute(TArgs args);

    }
    public readonly struct Unit
    {
        public static readonly Unit Value = new();
    }
    public record ProcessPhotoArgs(TaskItem Task);
    public class ProcessPhoto : TaskProcessor<ProcessPhotoArgs, Unit>
    {
        public override async Task<Unit> Execute(ProcessPhotoArgs args)
        {
            int delay = Convert.ToInt32(args.Task.Parameters);

            await Task.Delay(delay);

            await _camera.SetMode(GoProCameraFake.CameraStatus.Photo);
            await new TakePhoto().Execute(Unit.Value);

            await new SendMedia().Execute(new SendMediaArgs(args.Task.ChatId, "ogo"));
            return Unit.Value;
        }
    }
    public record ProcessTimelapseArgs(string TimelapseDelay, List<long> Users);
    public class ProcessTimelapse : TaskProcessor<ProcessTimelapseArgs, Unit>
    {
        public override async Task<Unit> Execute(ProcessTimelapseArgs args)
        {
            int timelapseDelay = Convert.ToInt32(args.TimelapseDelay);

            Log.Debug("Время съёмки в милисекундах: {TimelapseDelay}", timelapseDelay); 

            await _camera.SetMode(GoProCameraFake.CameraStatus.Timelapse);
            await _camera.StartTimeLapse();

            await Task.Delay(timelapseDelay);

            await _camera.StopTimeLapse();

            foreach (var userId in args.Users)
            {
                await new SendMedia().Execute(new SendMediaArgs(userId, "ogo"));
            }
            return Unit.Value;
        }
    }
    public class TakePhoto : TaskProcessor<Unit, Unit>
    {
        public override async Task<Unit> Execute(Unit args)
        {
            Log.Debug("Фото сделано👍👍👍");
            return Unit.Value;
        }
    }
    public class DownloadLastMedia : TaskProcessor<Unit, Stream>
                                    //теперь наследует, но много текста
                                    //лол
    {
        public override async Task<Stream> Execute(Unit args)
        {
            var stream = await _camera.DownloadLastMedia();
            Log.Debug("Медиа скачано");
            return stream;
        }
    }
    public record SendMediaArgs(long? User, string Message);
    public class SendMedia : TaskProcessor<SendMediaArgs, Unit>
    {
        public override async Task<Unit> Execute(SendMediaArgs args)
        {
            var stream = await new DownloadLastMedia().Execute(Unit.Value);

            Log.Debug("Отправка фото пользователю {userId}", args.User);
            await Telegramm.SendPhoto(args.User, stream, args.Message);
            Log.Debug("Медиа отправлено");

            return Unit.Value;
        }
    }
}
