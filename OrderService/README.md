# Order Service

## Overview
Order Service is a microservice responsible for managing orders in the distributed system using the **Saga Pattern** for orchestrating transactions across multiple services.

## Responsibilities
- Create order entries with **Pending** status
- Start and orchestrate the Saga workflow
- Track saga state across all steps
- Handle compensation logic on failures
- Coordinate with other microservices:
  - Order Details Service
  - Payment Service
  - Notification Service

## Architecture

### Saga Workflow (Orchestration)
1. **Customer places order** → Order Service creates order (Pending)
2. **Order Service** → Publishes `OrderCreatedEvent` to Order Details Service
3. **Order Details Service** → Responds with `OrderDetailsCompletedEvent`
4. **Order Service** → Publishes `ProcessPaymentCommand` to Payment Service
5. **Payment Service** → Responds with `PaymentCompletedEvent`
6. **Order Service** → Publishes `SendNotificationCommand` to Notification Service
7. **Notification Service** → Responds with `NotificationCompletedEvent`
8. **Order Service** → Marks order as **Completed**

### Compensation Logic (Failure Handling)
- **Order Details fails** → Cancel order, notify customer
- **Payment fails** → Rollback order details, mark order as Cancelled, notify customer
- **Notification fails** → Retry up to 3 times, log error, but order remains valid

## API Endpoints

### Create Order
```http
POST /api/orders
Content-Type: application/json

{
  "customerId": "CUST-001",
  "items": [
    {
      "productId": "PROD-001",
      "productName": "Laptop",
      "quantity": 1,
      "unitPrice": 999.99
    }
  ]
}
```

**Response:**
```json
{
  "orderId": 1,
  "sagaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "customerId": "CUST-001",
  "totalAmount": 999.99,
  "status": "OrderDetailsProcessing",
  "orderDate": "2024-01-15T10:30:00Z",
  "message": "Order created successfully and saga initiated"
}
```

### Get Order Status
```http
GET /api/orders/{orderId}
```

**Response:**
```json
{
  "orderId": 1,
  "customerId": "CUST-001",
  "totalAmount": 999.99,
  "status": "PaymentProcessing",
  "orderDate": "2024-01-15T10:30:00Z",
  "sagaStatus": {
    "sagaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "currentStep": "OrderDetailsCompleted",
    "isOrderDetailsCompleted": true,
    "isPaymentCompleted": false,
    "isNotificationCompleted": false,
    "errorMessage": null
  }
}
```

## Order Status Flow
1. `Pending` - Order created
2. `OrderDetailsProcessing` - Waiting for Order Details Service
3. `PaymentProcessing` - Order details completed, processing payment
4. `NotificationProcessing` - Payment completed, sending notification
5. `Completed` - All steps successful
6. `Cancelled` - Order cancelled due to failure
7. `Failed` - Unrecoverable error

## Database Schema

### Orders Table
| Column | Type | Description |
|--------|------|-------------|
| OrderId | int (PK) | Auto-increment primary key |
| CustomerId | string | Customer identifier |
| OrderDate | datetime | Order creation timestamp |
| TotalAmount | decimal(18,2) | Total order amount |
| Status | string | Current order status |
| CreatedAt | datetime | Record creation time |
| UpdatedAt | datetime | Last update time |

### SagaStates Table
| Column | Type | Description |
|--------|------|-------------|
| SagaId | guid (PK) | Unique saga identifier |
| OrderId | int | Associated order ID |
| CustomerId | string | Customer identifier |
| CurrentStep | string | Current saga step |
| Status | string | Saga status |
| StartedAt | datetime | Saga start time |
| CompletedAt | datetime | Saga completion time |
| IsOrderDetailsCompleted | bool | Order details step flag |
| IsPaymentCompleted | bool | Payment step flag |
| IsNotificationCompleted | bool | Notification step flag |
| ErrorMessage | string | Error details if any |
| RetryCount | int | Retry attempts counter |

## Events Published

### OrderCreatedEvent
Published when order is created, consumed by Order Details Service.

### ProcessPaymentCommand
Published after order details completion, consumed by Payment Service.

### SendNotificationCommand
Published after payment success, consumed by Notification Service.

### OrderCancelledEvent
Published when order is cancelled due to failure.

### OrderCompletedEvent
Published when entire saga completes successfully.

## Events Consumed

### OrderDetailsCompletedEvent
From Order Details Service indicating success/failure.

### PaymentCompletedEvent
From Payment Service indicating payment success/failure.

### NotificationCompletedEvent
From Notification Service indicating notification success/failure.

## Configuration

### Message Bus
Currently configured to use **InMemory** for development.

For production, update `Program.cs` to use:
- **RabbitMQ** (recommended for on-premise)
- **Azure Service Bus** (recommended for cloud)

### Database
Uses SQLite for development. Change connection string in `appsettings.json` for production databases (SQL Server, PostgreSQL, etc.).

## Running the Service

1. Restore packages:
```bash
dotnet restore
```

2. Run the service:
```bash
dotnet run
```

3. Access Swagger UI:
```
https://localhost:5001/swagger
```

## Testing Saga Flow

1. Create an order using POST /api/orders
2. Simulate Order Details Service by publishing `OrderDetailsCompletedEvent`
3. Simulate Payment Service by publishing `PaymentCompletedEvent`
4. Simulate Notification Service by publishing `NotificationCompletedEvent`
5. Check order status using GET /api/orders/{orderId}

## Dependencies
- .NET 10
- Entity Framework Core 9.0
- MassTransit 8.2.0
- MediatR 12.4.0
- SQLite

## Future Enhancements
- Add authentication/authorization
- Implement distributed tracing
- Add health checks
- Implement circuit breaker pattern
- Add retry policies with exponential backoff
