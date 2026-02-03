using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Serilog;
using Serilog.Events;

namespace GoProTimelapse
{
    public class Storage
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<Telegramm>();

        public Storage()
        {
        }
        
        public static async Task<Guid> SaveFile(byte[] data, string extenstion)
        {
            using var _db = new AppDbContext();

            var filename = Guid.NewGuid();
            await File.WriteAllBytesAsync(@"GoProPhotos\" + filename.ToString() + extenstion, data);//реальная камера возвращает массив байтиков
            Log.Debug("Файл сохранён на диск");

            var media = new MediaItem
            {
                FileName = filename,
                SaveTime = DateTimeOffset.Now,
                Extenstion = extenstion
            };

            _db.Media.Add(media);
            await _db.SaveChangesAsync();
            Log.Debug("Файл сохранён в бд");

            return filename;
        }
        public static async Task<Stream> GetFileFromGuid(string guid, string extension)
        {
            var placeholder = File.OpenRead(@"GoProPhotos\" + guid + extension);
            return placeholder;
        }

        public static async Task<Stream> GetLastFile(string extension)
        {
            using var _db = new AppDbContext();

            var lastMedia = _db.Media
                .Where(m => m.Extenstion == extension)
                .AsEnumerable() //потом может чутка переделаб
                .OrderByDescending(m => m.SaveTime.UtcDateTime)
                .FirstOrDefault();

            Log.Debug("найдено медиа с ключом {x}, временем сохранения {y}", lastMedia.FileName, lastMedia.SaveTime);
            Log.Debug("Путь: {ogo}", @"GoProPhotos\" + lastMedia.FileName + lastMedia.Extenstion);

            var media = File.OpenRead(@"GoProPhotos\" + lastMedia.FileName + lastMedia.Extenstion);
            return media;
        }
    }
}
