using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoProTimelapse
{
    public enum MediaType
    {
        Photo,
        Video
    }
    public enum TaskType
    {
        Photo,
        Timelapse,
        RenderVideo
    }

    //todo: добавить scheduled 
    public enum TaskStatus
    {
        Created,
        InProgress,
        Completed,
        Failed
    }
    public enum CameraStatus
    {
        Timelapse,
        Photo,
        Video
    }
    

}
