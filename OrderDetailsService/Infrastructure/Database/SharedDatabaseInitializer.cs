using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace OrderDetailsService.Infrastructure.Database
{
    public static class SharedDatabaseInitializer
    {
        public static void EnsureSharedDatabaseSchema(string connectionString, ILogger logger)
        {
            try
            {
                logger.LogInformation("Initializing shared database schema at: {ConnectionString}", connectionString);

                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                using var command = connection.CreateCommand();

                // Create all tables for both services
                command.CommandText = @"
                    -- OrderService Tables
                    CREATE TABLE IF NOT EXISTS Orders (
                        OrderId INTEGER PRIMARY KEY AUTOINCREMENT,
                        CustomerId TEXT NOT NULL,
                        TotalAmount REAL NOT NULL,
                        Status TEXT NOT NULL,
                        OrderDate TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT
                    );

                    CREATE TABLE IF NOT EXISTS SagaStates (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        SagaId TEXT NOT NULL UNIQUE,
                        OrderId INTEGER NOT NULL,
                        CustomerId TEXT NOT NULL,
                        CurrentStep TEXT NOT NULL,
                        Status TEXT NOT NULL,
                        IsOrderDetailsCompleted INTEGER NOT NULL DEFAULT 0,
                        IsPaymentCompleted INTEGER NOT NULL DEFAULT 0,
                        IsNotificationCompleted INTEGER NOT NULL DEFAULT 0,
                        StartedAt TEXT NOT NULL,
                        CompletedAt TEXT,
                        ErrorMessage TEXT,
                        RetryCount INTEGER NOT NULL DEFAULT 0
                    );

                    CREATE TABLE IF NOT EXISTS OutboxMessages (
                        Id TEXT PRIMARY KEY,
                        MessageType TEXT NOT NULL,
                        Payload TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL,
                        Processed INTEGER NOT NULL DEFAULT 0,
                        ProcessedAt TEXT,
                        Attempts INTEGER NOT NULL DEFAULT 0,
                        LastError TEXT,
                        LockToken TEXT,
                        LockExpiresAt TEXT
                    );

                    -- OrderDetailsService Tables
                    CREATE TABLE IF NOT EXISTS OrderItems (
                        OrderItemId INTEGER PRIMARY KEY AUTOINCREMENT,
                        OrderId INTEGER NOT NULL,
                        ProductId TEXT NOT NULL,
                        ProductName TEXT NOT NULL,
                        Quantity INTEGER NOT NULL,
                        UnitPrice REAL NOT NULL,
                        TotalPrice REAL NOT NULL,
                        Status TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT
                    );

                    -- Create Indexes
                    CREATE INDEX IF NOT EXISTS IX_Orders_CustomerId ON Orders(CustomerId);
                    CREATE INDEX IF NOT EXISTS IX_SagaStates_OrderId ON SagaStates(OrderId);
                    CREATE INDEX IF NOT EXISTS IX_SagaStates_SagaId ON SagaStates(SagaId);
                    CREATE INDEX IF NOT EXISTS IX_OutboxMessages_Processed ON OutboxMessages(Processed);
                    CREATE INDEX IF NOT EXISTS IX_OutboxMessages_LockExpiresAt ON OutboxMessages(LockExpiresAt);
                    CREATE INDEX IF NOT EXISTS IX_OrderItems_OrderId ON OrderItems(OrderId);
                    CREATE INDEX IF NOT EXISTS IX_OrderItems_ProductId ON OrderItems(ProductId);
                ";

                command.ExecuteNonQuery();

                logger.LogInformation("Shared database schema initialized successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize shared database schema");
                throw;
            }
        }
    }
}
