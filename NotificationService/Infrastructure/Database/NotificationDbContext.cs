using Microsoft.EntityFrameworkCore;
using NotificationService.Domain;

namespace NotificationService.Infrastructure.Database
{
    public class NotificationDbContext : DbContext
    {
        public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
        {
        }

        public DbSet<Notification> Notifications { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Notification>()
                .HasIndex(n => n.SagaId);

            modelBuilder.Entity<Notification>()
                .HasIndex(n => n.OrderId);

            modelBuilder.Entity<OutboxMessage>()
                .HasIndex(o => o.Processed);

            modelBuilder.Entity<OutboxMessage>()
                .HasIndex(o => new { o.MessageType, o.Processed });
        }
    }
}
