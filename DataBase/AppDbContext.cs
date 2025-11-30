using Microsoft.EntityFrameworkCore;

namespace GoProTimelapse
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<TaskItem> Tasks { get; set; }

        public AppDbContext() { }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }
            
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite("Data Source=app.db");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .Property(u => u.IsAdmin)
                .HasDefaultValue(false);

            modelBuilder.Entity<User>()
                .Property(u => u.SunsetSubscribtion)
                .HasDefaultValue(false);

            modelBuilder.Entity<User>()
                .Property(u => u.TGUserId)
                .IsRequired();

            modelBuilder.Entity<TaskItem>(entity =>
            {
                entity.ToTable("Tasks");

                entity.HasKey(t => t.Id);

                // Храним enum как строку
                entity.Property(t => t.Type)
                      .HasConversion<string>()
                      .IsRequired();

                entity.Property(t => t.Status)
                      .HasConversion<string>()
                      .IsRequired();

                entity.Property(t => t.Parameters)
                      .HasColumnType("TEXT");

                entity.Property(t => t.CreatedAt)
                      .IsRequired();

                // Nullable поля для начала/окончания/планового времени
                entity.Property(t => t.StartedAt);
                entity.Property(t => t.FinishedAt);
                entity.Property(t => t.ScheduledAt);
                entity.Property(t => t.ChatId);
                entity.Property(t => t.UserId);
                entity.Property(t => t.Parameters);
            });
        }
    }
}
