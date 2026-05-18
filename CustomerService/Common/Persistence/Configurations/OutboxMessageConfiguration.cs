using CustomerService.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerService.Common.Persistence.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.MessageType).HasMaxLength(200);

        builder.HasIndex(o => o.Processed)
            .HasDatabaseName("IX_OutboxMessages_Processed");

        builder.HasIndex(o => o.LockExpiresAt)
            .HasDatabaseName("IX_OutboxMessages_LockExpiresAt");
    }
}
