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

            base.OnModelCreating(modelBuilder);
        }
    }
}
