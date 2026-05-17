# NotificationService

## Overview
NotificationService is the final step in the order saga orchestration. It receives `SendNotificationCommand` from OrderService via the database outbox pattern, sends mock notifications (email/SMS/push), and publishes `NotificationCompletedEvent` back to complete the saga.

## Architecture

### Database Outbox Pattern
- **Consumes**: `SendNotificationCommand` from shared OutboxMessages table
- **Produces**: `NotificationCompletedEvent` to shared OutboxMessages table
- **Database**: Shared SQLite database at `C:\Users\2493103\source\repos\CaseStudy\orderdetails.db`

### Background Services
- **OutboxConsumer**: Polls OutboxMessages table every 5 seconds for unprocessed SendNotificationCommand messages

### Mock Implementation
Currently, the notification service is a mock that:
- Logs notification messages
- Always succeeds
- Records notification attempts in the Notifications table
- Generates mock email addresses (`customerId@example.com`)

## Domain Model

### Notification
```csharp
public class Notification
{
    public int NotificationId { get; set; }
    public Guid SagaId { get; set; }
    public int OrderId { get; set; }
    public string CustomerId { get; set; }
    public string Message { get; set; }
    public string NotificationType { get; set; } // Email, SMS, Push
    public string Status { get; set; } // Pending, Sent, Failed
    public string? Recipient { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
}
```

## Saga Flow

1. **OrderService** publishes `SendNotificationCommand` to outbox (via MassTransit in-memory)
2. **NotificationService.OutboxConsumer** polls and finds the command
3. Creates `Notification` record with "Sending" status
4. Simulates notification sending (mock - always succeeds)
5. Updates notification status to "Sent" or "Failed"
6. Publishes `NotificationCompletedEvent` to outbox
7. **OrderService.OutboxConsumer** picks up the event
8. **OrderSagaOrchestrator.HandleNotificationCompleted** marks saga as complete
9. Order status transitions to "Completed"

## API Endpoints

### GET /api/diagnostics/status
Returns notification service statistics:
```json
{
  "totalNotifications": 10,
  "sentNotifications": 10,
  "failedNotifications": 0,
  "pendingCommands": 0,
  "pendingEvents": 0
}
```

### GET /api/diagnostics/notifications?orderId=123
Returns notifications for a specific order or all recent notifications.

### GET /api/diagnostics/outbox?messageType=SendNotificationCommand
Returns outbox messages filtered by type.

### GET /api/diagnostics/notification/{id}
Returns detailed information about a specific notification.

## Configuration

### appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=C:\\Users\\2493103\\source\\repos\\CaseStudy\\orderdetails.db"
  }
}
```

## Running the Service

1. **Start NotificationService**:
   ```bash
   cd NotificationService
   dotnet run
   ```
   Service runs on: `http://localhost:5005`

2. **View Swagger UI**:
   Navigate to `http://localhost:5005/swagger`

3. **Check Status**:
   ```bash
   curl http://localhost:5005/api/diagnostics/status
   ```

## Testing the Saga

### Complete Saga Flow Test
1. Start all services (OrderService, OrderDetailsService, PaymentService, NotificationService)
2. Create an order via OrderService
3. Watch logs to see saga progression:
   - OrderDetailsProcessing → PaymentProcessing → NotificationProcessing → Completed

### Example Test
```bash
# Create order
curl -X POST http://localhost:5001/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "CUST001",
    "totalAmount": 250.00,
    "orderItems": [
      {"productId": "PROD001", "quantity": 2, "unitPrice": 125.00}
    ]
  }'

# Check order status (should eventually reach "Completed")
curl http://localhost:5001/api/orders/{orderId}

# Check notification service status
curl http://localhost:5005/api/diagnostics/status

# View notifications for the order
curl http://localhost:5005/api/diagnostics/notifications?orderId={orderId}
```

## Notification Types

The service supports three notification types (all mocked):

### Email (Default)
- Mock recipient: `{customerId}@example.com`
- Always succeeds

### SMS
- Mock recipient: Customer phone number
- Always succeeds

### Push
- Mock recipient: Device token
- Always succeeds

## Database Tables

### Notifications
Stores all notification attempts:
```sql
CREATE TABLE Notifications (
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
```

### OutboxMessages (Shared)
Same table used by all services for cross-service communication.

## Future Enhancements

1. **Real Email Integration**:
   - SendGrid, AWS SES, Mailgun
   - HTML templates
   - Attachments

2. **Real SMS Integration**:
   - Twilio, AWS SNS
   - SMS templates
   - Delivery confirmations

3. **Push Notifications**:
   - Firebase Cloud Messaging
   - Apple Push Notification Service
   - Web push

4. **Retry Logic**:
   - Exponential backoff for failures
   - Dead letter queue for persistent failures
   - Manual retry endpoints

5. **Templates**:
   - Template engine (Razor, Handlebars)
   - Multi-language support
   - Personalization

## Troubleshooting

### No notifications being sent
1. Check if NotificationService is running: `curl http://localhost:5005/api/diagnostics/status`
2. Check for unprocessed commands: `curl http://localhost:5005/api/diagnostics/outbox?messageType=SendNotificationCommand`
3. Check NotificationService logs for errors
4. Verify SendNotificationCommand is being published by OrderService

### Notifications sent but saga not completing
1. Check for NotificationCompletedEvent in outbox: `curl http://localhost:5005/api/diagnostics/outbox?messageType=NotificationCompletedEvent`
2. Check if OrderService is consuming the event (check OrderService logs)
3. Verify OrderSagaOrchestrator.HandleNotificationCompleted is being invoked

### Database locked errors
- Ensure only one instance of each service is running
- Check for long-running transactions in logs
- Restart all services in order: NotificationService, PaymentService, OrderDetailsService, OrderService
