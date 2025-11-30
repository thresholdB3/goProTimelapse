using Microsoft.EntityFrameworkCore;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Data;
using Microsoft.VisualBasic;
using Newtonsoft.Json;

namespace GoProTimelapse
{
    public class SunsetPlanner
    {
        private readonly AppDbContext _db;
        //потом вынести в appsettings
        private readonly double latitude;
        private readonly double longitude;
        private object template;

        public SunsetPlanner()
        {
            _db = new AppDbContext();
            latitude = 56.8386;   //широта
            longitude = 60.6055;  //долгота
            template = new
            {
                results = new
                {
                    sunset = "",
                    civil_twilight_end = ""
                }
            };
        }
        public async Task StartAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Console.WriteLine("ogo");
                await GetSunsetTime();
                await Task.Delay(TimeSpan.FromDays(1));
                Console.WriteLine("Планировщик таймлапсов запущен");
            }
        }
        public async Task GetSunsetTime()
        {
            string apiUrl = $"https://api.sunrise-sunset.org/json?lat={latitude}&lng={longitude}&formatted=0";

            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeAnonymousType(jsonResponse, template);
                    dynamic data2 = JsonConvert.DeserializeObject(jsonResponse);

                    DateTime sunset = DateTime.Parse((string)data.sunset);
                    DateTime civilTwilightEnd = DateTime.Parse((string)data.results.civil_twilight_end);
                    Console.WriteLine(template);
                    Console.WriteLine(data);
                    Console.WriteLine(data2);

                    // Console.WriteLine($"Начало заката (заход солнца): {sunset}"); //а теперь вроде правильное :O
                    // Console.WriteLine($"Конец заката (окончание гражданских сумерек): {civilTwilightEnd}");
                }
            }

        }

        public async Task ScheduleTimelapse(DateTime sunsetTime)
        {
            var task = new TaskItem
            {
                Type = TaskType.Timelapse,
                Status = TaskStatus.Created,
                CreatedAt = DateTime.UtcNow,
                ScheduledAt = sunsetTime.AddHours(1).AddMinutes(10)
            };
            _db.Tasks.Add(task);
            await _db.SaveChangesAsync();

            await Worker.NotifyNewTask();
            Console.WriteLine("Задача создана");
        }
    }
}