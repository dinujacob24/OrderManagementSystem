using Microsoft.EntityFrameworkCore;
using PaymentService.Domain;

namespace PaymentService.Infrastructure.Database
{
    public class PaymentDbContext : DbContext
    {
        public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
        {
        }

        public DbSet<Payment> Payments { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Payment>()
                .HasIndex(p => p.SagaId);

            modelBuilder.Entity<Payment>()
                .HasIndex(p => p.OrderId);

            modelBuilder.Entity<OutboxMessage>()
                .HasIndex(o => o.Processed);

            modelBuilder.Entity<OutboxMessage>()
                .HasIndex(o => new { o.MessageType, o.Processed });
        }
    }
}
