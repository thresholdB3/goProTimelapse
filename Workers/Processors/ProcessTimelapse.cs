using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoProTimelapse
{
    public class ProcessTimelapseArgs: ProcessorArgs
    {
        public ProcessTimelapseArgs(string timelapseDelay, List<long> users)
        {
            TimelapseDelay = timelapseDelay;
            Users = users;
        }

        public string TimelapseDelay { get; set; }
        public List<long> Users { get; set; }
    }
    public class ProcessTimelapse : TaskProcessor
    {
        public override async Task Execute(ProcessorArgs? args = null)
        {
            var myArgs = args as ProcessTimelapseArgs;
            var timelapseDelay = TimeSpan.Parse(myArgs.TimelapseDelay);

            Log.Debug("Время съёмки в милисекундах: {TimelapseDelay}", timelapseDelay);

            await _camera.MakeTimelapse(timelapseDelay);
            await Task.Delay(5000);

            var media = await _camera.DownloadLastMedia(".mp4");
            //await Storage.SaveFile(media, ".mp4");
            Stream stream = new MemoryStream(media);

            foreach (var userId in myArgs.Users)
            {
                await new SendMedia().Execute(new SendMediaArgs(userId, "ogo", MediaType.Video, stream));
            }
        }

        
        
    }
}
