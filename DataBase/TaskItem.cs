using System;
using System.Text.Json;

namespace GoProTimelapse
{
    

    public class TaskItem
    {
        public int Id { get; set; }                   // PK
        public TaskType Type { get; set; }           // Тип задачи
        public TaskStatus Status { get; set; }       // Статус
        public string? Parameters { get; set; }       // JSON-параметры (fps, duration и т.д.)
        public int? UserId { get; set; }             // Кто создал
        public DateTimeOffset CreatedAt { get; set; }      // Когда создана
        public DateTimeOffset? StartedAt { get; set; }     // Когда начата
        public DateTimeOffset? FinishedAt { get; set; }    // Когда завершена
        public DateTimeOffset? ScheduledAt { get; set; }   // Плановое время начала

        public long? ChatId { get; set; }
        // Удобные методы для сериализации параметров
        public T GetParameters<T>() //а оно мне надо блин?
        {
            if (string.IsNullOrWhiteSpace(Parameters)) return default!;
            return JsonSerializer.Deserialize<T>(Parameters);
        }

        public void SetParameters<T>(T obj)//и это
        {
            Parameters = JsonSerializer.Serialize(obj);
        }
    }
}
