using Microsoft.EntityFrameworkCore;
using PaymentService.Infrastructure.Database;

namespace PaymentService.Infrastructure.Database
{
    public static class SharedDatabaseInitializer
    {
        public static async Task InitializeAsync(PaymentDbContext context)
        {
            // Create Payments table if it doesn't exist
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS Payments (
                    PaymentId INTEGER PRIMARY KEY AUTOINCREMENT,
                    SagaId TEXT NOT NULL,
                    OrderId INTEGER NOT NULL,
                    CustomerId TEXT NOT NULL,
                    Amount REAL NOT NULL,
                    Status TEXT NOT NULL DEFAULT 'Pending',
                    PaymentMethod TEXT,
                    TransactionId TEXT,
                    ErrorMessage TEXT,
                    CreatedAt TEXT NOT NULL,
                    ProcessedAt TEXT
                );

                CREATE INDEX IF NOT EXISTS IX_Payments_SagaId ON Payments(SagaId);
                CREATE INDEX IF NOT EXISTS IX_Payments_OrderId ON Payments(OrderId);
            ");

            // OutboxMessages table is already created by OrderService
            // But we'll ensure it exists
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS OutboxMessages (
                    Id TEXT PRIMARY KEY,
                    MessageType TEXT NOT NULL,
                    Payload TEXT NOT NULL,
                    Processed INTEGER NOT NULL DEFAULT 0,
                    CreatedAt TEXT NOT NULL,
                    ProcessedAt TEXT,
                    Attempts INTEGER NOT NULL DEFAULT 0,
                    LastError TEXT,
                    LockToken TEXT,
                    LockExpiresAt TEXT
                );

                CREATE INDEX IF NOT EXISTS IX_OutboxMessages_Processed ON OutboxMessages(Processed);
                CREATE INDEX IF NOT EXISTS IX_OutboxMessages_MessageType_Processed ON OutboxMessages(MessageType, Processed);
            ");
        }
    }
}
