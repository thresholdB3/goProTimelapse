using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoProTimelapse
{
 
    public class SendMediaArgs: ProcessorArgs
    {
        public SendMediaArgs(long? user, string message, MediaType type, Stream media)
        {
            User = user;
            Message = message;
            Type = type;
            Media = media;
        }

        public long? User { get; set; }
        public string Message { get; set; }
        public MediaType Type { get; set; }
        public Stream Media { get; set; }
    }
    public class SendMedia : TaskProcessor
    {
        public override async Task Execute(ProcessorArgs? args = null)
        {
            var smArgs = args as SendMediaArgs;
            Log.Debug("Отправка фото пользователю {userId}", smArgs.User);
            await Telegramm.SendMedia(smArgs.User, smArgs.Media, smArgs.Message, smArgs.Type);
            Log.Debug("Медиа отправлено");
        }
    }
}
