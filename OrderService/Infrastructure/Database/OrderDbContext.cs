using Microsoft.EntityFrameworkCore;
using OrderService.Domain;

namespace OrderService.Infrastructure.Database
{
    public class OrderDbContext : DbContext
    {
        public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
        {
        }

        public DbSet<Order> Orders { get; set; }
        public DbSet<SagaState> SagaStates { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>()
                .Property(o => o.TotalAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.CustomerId);

            modelBuilder.Entity<SagaState>()
                .HasIndex(s => s.OrderId);

            modelBuilder.Entity<OutboxMessage>()
                .HasIndex(o => o.Processed)
                .HasDatabaseName("IX_OutboxMessages_Processed");

            modelBuilder.Entity<OutboxMessage>()
                .HasIndex(o => o.LockExpiresAt)
                .HasDatabaseName("IX_OutboxMessages_LockExpiresAt");

            modelBuilder.Entity<OutboxMessage>()
                .Property(o => o.MessageType)
                .HasMaxLength(200);

            base.OnModelCreating(modelBuilder);
        }
    }
}
