using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoProTimelapse
{
    public class ProcessPhotoArgs : ProcessorArgs
    {
        public ProcessPhotoArgs(TaskItem task) 
        {
            Task = task;
        }
        public TaskItem Task { get; set; }

    }

    public class ProcessPhoto : TaskProcessor
    {
        public override async Task Execute(ProcessorArgs? args)
        {
            var myArgs = args as ProcessPhotoArgs;
            int delay = Convert.ToInt32(myArgs.Task.Parameters);

            await Task.Delay(delay);

            await _camera.TakePhoto();

            await Task.Delay(5000);

            var media = await _camera.DownloadLastMedia(".jpg");
            Stream stream = new MemoryStream(media);

            await new SendMedia().Execute(new SendMediaArgs(myArgs.Task.ChatId, "ogo", MediaType.Photo, stream));
        }
    }
}
