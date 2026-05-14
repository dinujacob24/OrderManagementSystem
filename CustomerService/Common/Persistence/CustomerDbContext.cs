using CustomerService.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace CustomerService.Common.Persistence;

public class CustomerDbContext : DbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public CustomerDbContext(DbContextOptions<CustomerDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomerDbContext).Assembly);
    }
}
