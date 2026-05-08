# Quick Start Guide - Order Service

## Project Structure

```
OrderService/
├── Domain/                    # Domain entities
│   ├── Order.cs              # Order entity (no items)
│   ├── OrderStatus.cs        # Status constants
│   └── SagaState.cs          # Saga state tracking
├── Infrastructure/
│   └── Database/
│       └── OrderDbContext.cs # EF Core DbContext
├── Features/                 # CQRS features
│   ├── CreateOrder/
│   │   ├── CreateOrderController.cs
│   │   └── CreateOrderCommandHandler.cs
│   └── GetOrderStatus/
│       └── GetOrderStatusQueryHandler.cs
├── Saga/
│   └── OrderSagaOrchestrator.cs  # Saga orchestration logic
├── Consumers/                # Message consumers
│   ├── OrderDetailsCompletedConsumer.cs
│   ├── PaymentCompletedConsumer.cs
│   └── NotificationCompletedConsumer.cs
├── Messages/
│   ├── Events/               # Domain events
│   │   ├── OrderCreatedEvent.cs
│   │   ├── OrderDetailsCompletedEvent.cs
│   │   ├── PaymentCompletedEvent.cs
│   │   ├── NotificationCompletedEvent.cs
│   │   ├── OrderCancelledEvent.cs
│   │   └── OrderCompletedEvent.cs
│   └── Commands/             # Commands to other services
│       ├── ProcessPaymentCommand.cs
│       └── SendNotificationCommand.cs
└── DTOs/                     # Data transfer objects
    ├── CreateOrderRequest.cs
    ├── CreateOrderResponse.cs
    └── OrderStatusResponse.cs
```

## Key Design Principles

### 1. **Separation of Concerns**
- **Order Service** only manages orders (not order items)
- **Order Details Service** (separate) handles product details, quantities, pricing
- Clean separation via events and commands

### 2. **Saga Orchestration**
- Order Service acts as the **Saga Orchestrator**
- Coordinates workflow across 4 microservices:
  - Order Details Service
  - Payment Service
  - Notification Service
  - Customer Service (validation)

### 3. **Event-Driven Architecture**
- Uses MassTransit for message bus (InMemory for dev, RabbitMQ/Azure Service Bus for prod)
- Publishes events when state changes
- Consumes events from other services

## Running the Service

### 1. Restore Dependencies
```bash
cd OrderService
dotnet restore
```

### 2. Run the Service
```bash
dotnet run
```

The service will start on:
- HTTPS: `https://localhost:5001`
- HTTP: `http://localhost:5000`

### 3. Access Swagger UI
Open your browser: `https://localhost:5001/swagger`

## Testing the Saga Flow

### Step 1: Create an Order
```bash
curl -X POST https://localhost:5001/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "CUST-001",
    "items": [
      {
        "productId": "PROD-001",
        "productName": "Laptop",
        "quantity": 1,
        "unitPrice": 999.99
      },
      {
        "productId": "PROD-002",
        "productName": "Mouse",
        "quantity": 2,
        "unitPrice": 29.99
      }
    ]
  }'
```

**Response:**
```json
{
  "orderId": 1,
  "sagaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "customerId": "CUST-001",
  "totalAmount": 1059.97,
  "status": "OrderDetailsProcessing",
  "orderDate": "2024-01-15T10:30:00Z",
  "message": "Order created successfully and saga initiated"
}
```

### Step 2: Check Order Status
```bash
curl https://localhost:5001/api/orders/1
```

**Response:**
```json
{
  "orderId": 1,
  "customerId": "CUST-001",
  "totalAmount": 1059.97,
  "status": "OrderDetailsProcessing",
  "orderDate": "2024-01-15T10:30:00Z",
  "sagaStatus": {
    "sagaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "currentStep": "OrderCreated",
    "isOrderDetailsCompleted": false,
    "isPaymentCompleted": false,
    "isNotificationCompleted": false,
    "errorMessage": null
  }
}
```

## Events Published by Order Service

### 1. OrderCreatedEvent
**When:** Order is created
**Consumed By:** Order Details Service
**Purpose:** Instruct Order Details Service to store product details

```json
{
  "sagaId": "guid",
  "orderId": 1,
  "customerId": "CUST-001",
  "items": [
    {
      "productId": "PROD-001",
      "productName": "Laptop",
      "quantity": 1,
      "unitPrice": 999.99
    }
  ],
  "totalAmount": 999.99,
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### 2. ProcessPaymentCommand
**When:** Order details completed successfully
**Consumed By:** Payment Service
**Purpose:** Request payment processing

```json
{
  "sagaId": "guid",
  "orderId": 1,
  "customerId": "CUST-001",
  "amount": 999.99,
  "timestamp": "2024-01-15T10:31:00Z"
}
```

### 3. SendNotificationCommand
**When:** Payment completed successfully
**Consumed By:** Notification Service
**Purpose:** Send order confirmation to customer

```json
{
  "sagaId": "guid",
  "orderId": 1,
  "customerId": "CUST-001",
  "message": "Your order #1 has been successfully placed!",
  "notificationType": "OrderConfirmation",
  "timestamp": "2024-01-15T10:32:00Z"
}
```

### 4. OrderCancelledEvent
**When:** Any step fails (order details or payment)
**Consumed By:** Notification Service
**Purpose:** Notify customer of cancellation

```json
{
  "sagaId": "guid",
  "orderId": 1,
  "customerId": "CUST-001",
  "reason": "Payment processing failed",
  "timestamp": "2024-01-15T10:31:30Z"
}
```

### 5. OrderCompletedEvent
**When:** All steps complete successfully
**Consumed By:** Analytics/Reporting services
**Purpose:** Record successful order completion

```json
{
  "sagaId": "guid",
  "orderId": 1,
  "customerId": "CUST-001",
  "totalAmount": 999.99,
  "timestamp": "2024-01-15T10:33:00Z"
}
```

## Events Consumed by Order Service

### 1. OrderDetailsCompletedEvent
**From:** Order Details Service
**Action:** 
- If success → Publish ProcessPaymentCommand
- If failure → Compensate (cancel order)

```json
{
  "sagaId": "guid",
  "orderId": 1,
  "success": true,
  "errorMessage": null,
  "timestamp": "2024-01-15T10:31:00Z"
}
```

### 2. PaymentCompletedEvent
**From:** Payment Service
**Action:**
- If success → Publish SendNotificationCommand
- If failure → Compensate (rollback order details, cancel order)

```json
{
  "sagaId": "guid",
  "orderId": 1,
  "transactionId": "TXN-12345",
  "success": true,
  "errorMessage": null,
  "timestamp": "2024-01-15T10:32:00Z"
}
```

### 3. NotificationCompletedEvent
**From:** Notification Service
**Action:**
- If success → Mark order as Completed
- If failure → Retry up to 3 times (order still valid)

```json
{
  "sagaId": "guid",
  "orderId": 1,
  "success": true,
  "errorMessage": null,
  "timestamp": "2024-01-15T10:33:00Z"
}
```

## Database Tables

### Orders
```sql
CREATE TABLE Orders (
    OrderId INTEGER PRIMARY KEY AUTOINCREMENT,
    CustomerId TEXT NOT NULL,
    OrderDate DATETIME NOT NULL,
    TotalAmount DECIMAL(18,2) NOT NULL,
    Status TEXT NOT NULL,
    CreatedAt DATETIME NOT NULL,
    UpdatedAt DATETIME
);
```

### SagaStates
```sql
CREATE TABLE SagaStates (
    SagaId TEXT PRIMARY KEY,
    OrderId INTEGER NOT NULL,
    CustomerId TEXT NOT NULL,
    CurrentStep TEXT NOT NULL,
    Status TEXT NOT NULL,
    StartedAt DATETIME NOT NULL,
    CompletedAt DATETIME,
    IsOrderDetailsCompleted BIT NOT NULL,
    IsPaymentCompleted BIT NOT NULL,
    IsNotificationCompleted BIT NOT NULL,
    ErrorMessage TEXT,
    RetryCount INTEGER NOT NULL
);
```

## Configuration

### Switch to RabbitMQ (Production)

1. Update `Program.cs`:
```csharp
// Comment out InMemory, uncomment RabbitMQ
x.UsingRabbitMq((context, cfg) =>
{
    cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
    {
        h.Username(builder.Configuration["RabbitMQ:Username"]);
        h.Password(builder.Configuration["RabbitMQ:Password"]);
    });
    cfg.ConfigureEndpoints(context);
});
```

2. Install RabbitMQ:
```bash
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

3. Update `appsettings.json`:
```json
"RabbitMQ": {
  "Host": "localhost",
  "Username": "guest",
  "Password": "guest"
}
```

### Switch to Azure Service Bus (Cloud)

1. Update `Program.cs`:
```csharp
x.UsingAzureServiceBus((context, cfg) =>
{
    cfg.Host(builder.Configuration["AzureServiceBus:ConnectionString"]);
    cfg.ConfigureEndpoints(context);
});
```

2. Update `appsettings.json`:
```json
"AzureServiceBus": {
  "ConnectionString": "Endpoint=sb://your-namespace.servicebus.windows.net/..."
}
```

## Next Steps

1. **Build Order Details Service** - To handle product details
2. **Build Payment Service** - To process payments
3. **Build Notification Service** - To send notifications
4. **Build Customer Service** - To validate customers
5. **Add Authentication** - JWT Bearer tokens
6. **Add Health Checks** - Monitor service health
7. **Add Distributed Tracing** - OpenTelemetry
8. **Add Resilience** - Circuit breakers, retry policies

## Common Issues

### Issue: Database not created
**Solution:** The database is automatically created on first run. Check for `orderservice.db` in the project directory.

### Issue: Events not being consumed
**Solution:** Ensure all consumer services are running and connected to the same message bus.

### Issue: Saga stuck in processing
**Solution:** Check the SagaStates table for error messages and retry counts.

## Support

For issues or questions, check:
- README.md for detailed documentation
- Swagger UI for API testing
- Application logs for debugging
