using Microsoft.EntityFrameworkCore;
using NotificationService.Infrastructure.Database;

namespace NotificationService.Infrastructure.Database
{
    public static class SharedDatabaseInitializer
    {
        public static async Task InitializeAsync(NotificationDbContext context)
        {
            // Create Notifications table if it doesn't exist
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS Notifications (
                    NotificationId INTEGER PRIMARY KEY AUTOINCREMENT,
                    SagaId TEXT NOT NULL,
                    OrderId INTEGER NOT NULL,
                    CustomerId TEXT NOT NULL,
                    Message TEXT NOT NULL,
                    NotificationType TEXT NOT NULL DEFAULT 'Email',
                    Status TEXT NOT NULL DEFAULT 'Pending',
                    Recipient TEXT,
                    ErrorMessage TEXT,
                    CreatedAt TEXT NOT NULL,
                    SentAt TEXT
                );

                CREATE INDEX IF NOT EXISTS IX_Notifications_SagaId ON Notifications(SagaId);
                CREATE INDEX IF NOT EXISTS IX_Notifications_OrderId ON Notifications(OrderId);
            ");

            // OutboxMessages table is already created by OrderService
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
