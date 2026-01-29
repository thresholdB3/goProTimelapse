using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoProTimelapse
{


    public class MediaItem
    {
        public int Id { get; set; }                   // PK
        public Guid FileName { get; set; }
        public DateTimeOffset SaveTime { get; set; }
        public string Extenstion { get; set; }
    }
}
