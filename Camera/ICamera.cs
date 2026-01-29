using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoProTimelapse
{
    internal interface ICamera
    {
        public bool isBusy { get; set; }
        extern private Task SetMode(CameraStatus mode);
        public Task TakePhoto(); 
        public Task MakeTimelapse(TimeSpan delay);
        public Task<byte[]> DownloadLastMedia(MediaType Type); //todo: +удаление с камеры
    }
}
