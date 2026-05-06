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
            try
            {
                Log.Information("Происходит обработка таймлапса...");
                var myArgs = args as ProcessTimelapseArgs;
                var timelapseDelay = TimeSpan.Parse(myArgs.TimelapseDelay);

                Log.Debug("Время съёмки в милисекундах: {TimelapseDelay}", timelapseDelay);

                await _camera.MakeTimelapse(timelapseDelay);
                await Task.Delay(5000);

                var media = await _camera.DownloadLastMedia(".mp4");
                var media1 = await FFMpegWorker.CompressAsync(media);
                Stream stream = new MemoryStream(media1);

                foreach (var userId in myArgs.Users)
                {
                    await new SendMedia().Execute(new SendMediaArgs(userId, "( ˘▽˘)っ♨ Таймлапс", MediaType.Video, stream));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Произошла ошибка при обработке таймлапса :(");
            }
        }



    }
}
