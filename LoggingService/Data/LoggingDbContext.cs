using LoggingService.Models;
using Microsoft.EntityFrameworkCore;

namespace LoggingService.Data
{
    public class LoggingDbContext : DbContext
    {
        public LoggingDbContext(DbContextOptions<LoggingDbContext> options) : base(options)
        {
        }

        public DbSet<LogEntry> Logs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LogEntry>(entity =>
            {
                entity.ToTable("Logs");
                entity.HasKey(e => e.LogId);
            });
        }
    }
}
