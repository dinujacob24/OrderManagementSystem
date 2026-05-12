using Microsoft.EntityFrameworkCore;
using OrderDetailsService.Domain;

namespace OrderDetailsService.Infrastructure.Database
{
    public class OrderDetailsDbContext : DbContext
    {
        public OrderDetailsDbContext(DbContextOptions<OrderDetailsDbContext> options) : base(options)
        {
        }

        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderItem>()
                .Property(o => o.UnitPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<OrderItem>()
                .Property(o => o.TotalPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<OrderItem>()
                .HasIndex(o => o.OrderId);

            modelBuilder.Entity<OrderItem>()
                .HasIndex(o => o.ProductId);

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
