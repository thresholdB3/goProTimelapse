using Microsoft.EntityFrameworkCore;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Data;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;

namespace GoProTimelapse
{
    public class SunsetPlanner
    {
        private readonly AppDbContext _db;
        private static readonly ILogger Log = Serilog.Log.ForContext<SunsetPlanner>();
        private readonly double latitude;
        private readonly double longitude;

        public SunsetPlanner()
        {
            _db = new AppDbContext();
            latitude = 56.8386;   //широта
            longitude = 60.6055;  //долгота
            //todo: вынести в appsettings широту долготу
        }
        public async Task StartAsync(CancellationToken token)
        {
            Log.Information("Запуск планировщика закатов...");
            await GetSunsetTime();
            await GetSunsetTime(1);
            while (!token.IsCancellationRequested)
            {
                await GetSunsetTime(2);
                await Task.Delay(TimeSpan.FromDays(1));
            }
        }
        public async Task GetSunsetTime(int daysAhead = 0)
        {
            Log.Information("Получение времени заката (смещение в днях: {DaysAhead})...", daysAhead);
            try
            {
                string targetDate = DateTime.Today.AddDays(daysAhead).ToString("yyyy-MM-dd");
                string apiUrl = $"https://api.sunrise-sunset.org/json?lat={latitude}&lng={longitude}&date={targetDate}&formatted=0&tzid=Asia/Yekaterinburg";

                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(apiUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        var template = new { results = new { sunset = "", civil_twilight_end = "" }, status = "" };
                        var data = JsonConvert.DeserializeAnonymousType(jsonResponse, template);

                        DateTimeOffset sunset = DateTimeOffset.Parse(data.results.sunset);
                        DateTimeOffset civilTwilightEnd = DateTimeOffset.Parse(data.results.civil_twilight_end);

                        Log.Debug("Начало заката: {Sunset}", sunset);
                        Log.Debug("Конец заката: {CivilTwilightEnd}", civilTwilightEnd);

                        if (sunset > DateTimeOffset.Now)
                        {
                            await ScheduleTimelapse(sunset, civilTwilightEnd);
                        }
                        //await ScheduleTimelapse(DateTimeOffset.Now.AddSeconds(3), DateTimeOffset.Now.AddSeconds(30));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка в планировщике закатов :(");
            }

        }

        public async Task ScheduleTimelapse(DateTimeOffset sunsetTime, DateTimeOffset sunsetTime2)
        {
            try
            {
                Log.Information("Создание задачи...");
                var timelapseTime = (sunsetTime2 - sunsetTime);
                Log.Debug("Время таймлапса: {timelapseTime}", timelapseTime);

                var task = new TaskItem
                {
                    Type = TaskType.Timelapse,
                    Status = TaskStatus.Created,
                    CreatedAt = DateTimeOffset.Now,
                    ScheduledAt = sunsetTime,
                    Parameters = timelapseTime.ToString()
                };

                _db.Tasks.Add(task);
                await _db.SaveChangesAsync();
                await Worker.NotifyNewTask();

                Log.Information("Задача создана!");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка в планировщике закатов :(");
            }
        }
    }
}