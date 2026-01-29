using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoProTimelapse
{
    public class TakePhoto : TaskProcessor
    {
        public override async Task Execute(ProcessorArgs? args = null)
        {
            Log.Debug("Фото сделано👍👍👍");
             await Task.CompletedTask;
        }

        
    }
}
