using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace GoProTimelapse
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // var settings = Settings.ReadSettings();
            // var wlanWorker = new WlanWorker(settings.Network);
            // var camera = new GoProCamera(settings);
            // var telegramm = new Telegramm();

            // wlanWorker.Connect(settings.GoPro.CameraSSID, settings.GoPro.CameraPassword);
            // await Task.Delay(3000); //3 секунды на подключение
            // await camera.SetPhotoModeAsync();

            // if (!Directory.Exists(settings.Base.DownloadFolder))
            //     Directory.CreateDirectory(settings.Base.DownloadFolder);

            // DateTime timeStart = DateTime.Now;
            // DateTime timeStop = new DateTime(2025, 8, 6, 15, 30, 0);
            // int photoCount = settings.GetPhotoCount(timeStart, timeStop);

            // //cписок для сохранённых локальных файлов
            // var photoFiles = new List<string>();

            // for (int i = 0; i < photoCount; i++)
            // {
            //     Console.WriteLine($"Съёмка фото {i + 1}/{photoCount}...");
            //     await camera.TakePhotoAsync();
            //     await camera.DownloadLastPhotoAsync(i.ToString() + ".jpg");
            //     photoFiles.Add(Path.Combine(settings.Base.DownloadFolder, i.ToString() + ".jpg"));
            //     await Task.Delay(settings.Timelaps.PhotoDelaySeconds - 11);//в коде уже есть задержка на 11 секунд
            //     //на самом деле не 11, надо тыкать и исправлять
            // }

            // wlanWorker.Connect(settings.Network.MainSSID, settings.Network.MainPassword);

            // string outputFileName = DateTime.Now.ToString("ssmmhh.ddMMyyyy") + ".mp4";

            // await FFMpegWorker.CreateVideoFromPhotos(photoFiles, settings.Base.DownloadFolder, outputFileName);

            // await telegramm.SendVideo(outputFileName, settings.Telegramm.botToken, int.Parse(settings.Telegramm.chatID));

            // Пример работы с CRUD
            var user = new User
            {
                Username = "ogo",
                FirstName = "Petya",
                LastName = "Volkov",
                RegisteredAt = DateTime.Now
            };

            CreateUser(user);
            var allUsers = GetAllUsers();
            Console.WriteLine("Пользователи:");
            foreach (var u in allUsers)
                Console.WriteLine($"{u.Id}: {u.Username} ({u.FirstName} {u.LastName})");

            user.FirstName = "Ne_Petya";
            UpdateUser(user);

            DeleteUser(user.Id);

        }
        
        static void CreateUser(User user)
        {
            using (var db = new AppDbContext())
            {
                db.Users.Add(user);
                db.SaveChanges();
                Console.WriteLine($"Добавлен пользователь {user.Username}");
            }
        }

        
        static List<User> GetAllUsers()
        {
            using (var db = new AppDbContext())
            {
                return db.Users.ToList();
            }
        }

        
        static void UpdateUser(User user)
        {
            using (var db = new AppDbContext())
            {
                db.Users.Update(user);
                db.SaveChanges();
                Console.WriteLine($"Обновлён пользователь {user.Username}");
            }
        }

        
        static void DeleteUser(int id)
        {
            using (var db = new AppDbContext())
            {
                var user = db.Users.FirstOrDefault(u => u.Id == id);
                if (user != null)
                {
                    db.Users.Remove(user);
                    db.SaveChanges();
                    Console.WriteLine($"Удалён пользователь {user.Username}");
                }
            }
        }
    }
}
