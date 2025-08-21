using Microsoft.EntityFrameworkCore;

namespace GoProTimelapse
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            //база данных лежит в файле app.db
            optionsBuilder.UseSqlite("Data Source=app.db");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //дефолты для bool полей
            modelBuilder.Entity<User>()
                .Property(u => u.IsAdmin)
                .HasDefaultValue(false);

            modelBuilder.Entity<User>()
                .Property(u => u.SunsetSubscribtion)
                .HasDefaultValue(false);
        }
    }
}

