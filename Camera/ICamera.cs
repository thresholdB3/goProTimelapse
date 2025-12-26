using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoProTimelapse
{
    //DD: тема интерфейсов (изучить)
    //для камер делаем вот такой интерфейс, работаем с ним так
    //интерфейс пусть реализует и фейковый класс, и настоящая камера.
    //когда появится доступ к камере - ты просто подменяешь реализацию с фейка на настоящю и вуаля, волшебство=)

    //описал пока очень абстрактно - конечно тут добавятся детали.
    //У меня , например, еще есть получение статуса камеры
    //и проверка - на связи ли она 

    internal interface ICamera
    {
        public bool isBusy { get; set; }
        public Task SetMode(GoProCameraFake.CameraStatus mode); //помоему как то не очень, потом переделаю
        public Task TakePhoto(); 
        public Task StartTimeLapse();
        public Task StopTimeLapse();
        public Task<Stream> DownloadLastMedia(); //+удаление с камеры

        // public Task<byte[]> GetLastVideo(); //+удаление с камеры //не одно и то же с тем что выше??


    }
}
