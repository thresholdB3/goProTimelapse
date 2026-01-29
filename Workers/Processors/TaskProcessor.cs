using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using Serilog.Events;

namespace GoProTimelapse
{
    public abstract class TaskProcessor
    {
        public readonly GoProCamera _camera;
        //private readonly Settings _settings; //??
        public readonly Storage _storage;
        protected ILogger Log => Serilog.Log.ForContext(GetType());
        public TaskProcessor()
        {
            _camera = GoProCamera.CreateSingleton();
            //_settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _storage = new Storage();
        }
       

        public abstract Task Execute(ProcessorArgs? args = null);

    }
    public class ProcessorArgs
    {

    }
    //public readonly struct Unit
    //{
    //    public static readonly Unit Value = new();
    //}
}
