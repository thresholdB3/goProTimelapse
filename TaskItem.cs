using System;
using System.Text.Json;

namespace GoProTimelapse
{
    public enum TaskType
    {
        Photo,
        Timelapse,
        RenderVideo
    }

    public enum TaskStatus
    {
        Created,
        InProgress,
        Completed,
        Failed
    }

    public class TaskItem
    {
        public int Id { get; set; }                   // PK
        public TaskType Type { get; set; }           // Тип задачи
        public TaskStatus Status { get; set; }       // Статус
        public string Parameters { get; set; }       // JSON-параметры (fps, duration и т.д.)
        public int? UserId { get; set; }             // Кто создал
        public DateTime CreatedAt { get; set; }      // Когда создана
        public DateTime? StartedAt { get; set; }     // Когда начата
        public DateTime? FinishedAt { get; set; }    // Когда завершена
        public DateTime? ScheduledAt { get; set; }   // Плановое время начала

        // Удобные методы для сериализации параметров
        public T GetParameters<T>()
        {
            if (string.IsNullOrWhiteSpace(Parameters)) return default!;
            return JsonSerializer.Deserialize<T>(Parameters);
        }

        public void SetParameters<T>(T obj)
        {
            Parameters = JsonSerializer.Serialize(obj);
        }
    }
}
