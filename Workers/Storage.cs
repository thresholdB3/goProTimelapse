using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoProTimelapse
{
    public class Storage
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<Storage>();

        public Storage()
        {
        }
        public static async Task<Guid> SaveFile(byte[] data, string extenstion)
        {
            try
            {
                using var _db = new AppDbContext();

                var filename = Guid.NewGuid();

                if (!Directory.Exists("GoProPhotos"))
                {
                    Directory.CreateDirectory("GoProPhotos");
                }
                await File.WriteAllBytesAsync(@"GoProPhotos\" + filename.ToString() + extenstion, data);//реальная камера возвращает массив байтиков

                var media = new MediaItem
                {
                    FileName = filename,
                    SaveTime = DateTimeOffset.Now,
                    Extenstion = extenstion
                };

                _db.Media.Add(media);
                await _db.SaveChangesAsync();
                Log.Information("Файл сохранён в бд и на диск");

                return filename;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Произошла ошибка при сохранении файла :(");
                return Guid.Empty;
            }
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

            Log.Information("найдено медиа с ключом {x}, временем сохранения {y}", lastMedia.FileName, lastMedia.SaveTime);
            Log.Information("Путь: {ogo}", @"GoProPhotos\" + lastMedia.FileName + lastMedia.Extenstion);

            var media = File.OpenRead(@"GoProPhotos\" + lastMedia.FileName + lastMedia.Extenstion);
            return media;
        }
    }
}
