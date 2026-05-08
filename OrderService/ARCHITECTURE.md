# Order Service - Architecture Diagrams

## System Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         CUSTOMER / CLIENT                            │
└────────────────────────────────┬────────────────────────────────────┘
                                 │ HTTP/REST
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                         ORDER SERVICE                                │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  API Layer (Controllers)                                      │  │
│  │  - POST /api/orders                                          │  │
│  │  - GET /api/orders/{id}                                      │  │
│  └─────────────┬────────────────────────────────────────────────┘  │
│                │                                                     │
│  ┌─────────────▼────────────────────────────────────────────────┐  │
│  │  Application Layer (CQRS Handlers)                           │  │
│  │  - CreateOrderCommandHandler                                 │  │
│  │  - GetOrderStatusQueryHandler                                │  │
│  └─────────────┬────────────────────────────────────────────────┘  │
│                │                                                     │
│  ┌─────────────▼────────────────────────────────────────────────┐  │
│  │  Saga Orchestrator                                           │  │
│  │  - StartOrderSaga()                                          │  │
│  │  - HandleOrderDetailsCompleted()                             │  │
│  │  - HandlePaymentCompleted()                                  │  │
│  │  - HandleNotificationCompleted()                             │  │
│  │  - CompensateOrder()                                         │  │
│  └─────────────┬────────────────────────────────────────────────┘  │
│                │                                                     │
│  ┌─────────────▼────────────────────────────────────────────────┐  │
│  │  Domain Layer                                                │  │
│  │  - Order (OrderId, CustomerId, TotalAmount, Status)          │  │
│  │  - SagaState (tracks workflow progress)                      │  │
│  └─────────────┬────────────────────────────────────────────────┘  │
│                │                                                     │
│  ┌─────────────▼────────────────────────────────────────────────┐  │
│  │  Database (SQLite / SQL Server)                              │  │
│  │  - Orders Table                                              │  │
│  │  - SagaStates Table                                          │  │
│  └──────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │
                    ┌─────────────┴─────────────┐
                    │  MESSAGE BUS (MassTransit) │
                    │  - RabbitMQ / Azure SB     │
                    └─────────────┬─────────────┘
                                  │
        ┌─────────────┬───────────┴───────────┬─────────────┐
        │             │                       │             │
        ▼             ▼                       ▼             ▼
┌───────────┐  ┌─────────────┐     ┌──────────────┐  ┌─────────────┐
│  Order    │  │  Payment    │     │ Notification │  │  Customer   │
│  Details  │  │  Service    │     │   Service    │  │  Service    │
│  Service  │  │             │     │              │  │             │
└───────────┘  └─────────────┘     └──────────────┘  └─────────────┘
```

## Saga Workflow Sequence

```
Customer                Order Service           Order Details       Payment         Notification
   │                         │                      Service          Service          Service
   │                         │                         │                │                │
   │  1. Create Order        │                         │                │                │
   ├────────────────────────>│                         │                │                │
   │                         │                         │                │                │
   │                    2. Create Order                │                │                │
   │                    (Status: Pending)              │                │                │
   │                         │                         │                │                │
   │                    3. Start Saga                  │                │                │
   │                         │                         │                │                │
   │                    4. Publish                     │                │                │
   │                    OrderCreatedEvent              │                │                │
   │                         ├────────────────────────>│                │                │
   │                         │                         │                │                │
   │                         │        5. Store Items   │                │                │
   │                         │           in DB         │                │                │
   │                         │                         │                │                │
   │                         │    6. OrderDetails      │                │                │
   │                         │      CompletedEvent     │                │                │
   │                         │<────────────────────────┤                │                │
   │                         │                         │                │                │
   │                    7. Update Order                │                │                │
   │                    (Status: PaymentProcessing)    │                │                │
   │                         │                         │                │                │
   │                    8. Publish                     │                │                │
   │                    ProcessPaymentCommand          │                │                │
   │                         ├────────────────────────────────────────>│                │
   │                         │                         │                │                │
   │                         │                         │   9. Process   │                │
   │                         │                         │      Payment   │                │
   │                         │                         │                │                │
   │                         │    10. PaymentCompletedEvent             │                │
   │                         │<────────────────────────────────────────┤                │
   │                         │                         │                │                │
   │                    11. Update Order               │                │                │
   │                    (Status: NotificationProcessing) │              │                │
   │                         │                         │                │                │
   │                    12. Publish                    │                │                │
   │                    SendNotificationCommand        │                │                │
   │                         ├───────────────────────────────────────────────────────>│
   │                         │                         │                │                │
   │                         │                         │                │   13. Send     │
   │                         │                         │                │    Notification│
   │                         │                         │                │                │
   │                         │    14. NotificationCompletedEvent        │                │
   │                         │<───────────────────────────────────────────────────────┤
   │                         │                         │                │                │
   │                    15. Update Order               │                │                │
   │                    (Status: Completed)            │                │                │
   │                         │                         │                │                │
   │  16. Response           │                         │                │                │
   │<────────────────────────┤                         │                │                │
   │                         │                         │                │                │
```

## Compensation Flow (Failure Scenario)

```
Order Service           Order Details       Payment         Notification
     │                      Service          Service          Service
     │                         │                │                │
     │  OrderCreatedEvent      │                │                │
     ├────────────────────────>│                │                │
     │                         │                │                │
     │                         X  FAILURE       │                │
     │                         │                │                │
     │  OrderDetailsCompleted  │                │                │
     │  Event (Success=false)  │                │                │
     │<────────────────────────┤                │                │
     │                         │                │                │
     │  COMPENSATION:          │                │                │
     │  1. Cancel Order        │                │                │
     │  2. Update Status       │                │                │
     │  3. Publish             │                │                │
     │     OrderCancelled      │                │                │
     │     Event               │                │                │
     │                         │                │                │
     │  4. Send Notification   │                │                │
     │     (Cancellation)      │                │                │
     ├───────────────────────────────────────────────────────>│
     │                         │                │                │
```

## Order Status State Machine

```
                           ┌──────────────┐
                           │   PENDING    │
                           └──────┬───────┘
                                  │
                      (Saga Started)
                                  │
                                  ▼
                    ┌──────────────────────────┐
                    │ ORDER DETAILS PROCESSING │
                    └────────┬────────┬─────────┘
                             │        │
              (Success)      │        │  (Failure)
                             │        │
                             ▼        ▼
            ┌────────────────────┐   ┌───────────┐
            │ PAYMENT PROCESSING │   │ CANCELLED │
            └────────┬────┬──────┘   └───────────┘
                     │    │
      (Success)      │    │  (Failure)
                     │    │
                     ▼    ▼
   ┌──────────────────────────┐   ┌───────────┐
   │ NOTIFICATION PROCESSING  │   │ CANCELLED │
   └────────┬─────────────────┘   └───────────┘
            │
    (Success/Retry)
            │
            ▼
    ┌─────────────┐
    │  COMPLETED  │
    └─────────────┘
```

## Data Flow - Order Creation

```
┌────────────────────────────────────────────────────────────────────┐
│ 1. Customer Request                                                 │
│    POST /api/orders                                                 │
│    {                                                               │
│      "customerId": "CUST-001",                                     │
│      "items": [                                                    │
│        { "productId": "PROD-001", "quantity": 1, "price": 999 }   │
│      ]                                                             │
│    }                                                               │
└────────────────────────────────┬───────────────────────────────────┘
                                 │
                                 ▼
┌────────────────────────────────────────────────────────────────────┐
│ 2. Order Service Processes                                          │
│    - Validates request                                             │
│    - Calculates total amount                                       │
│    - Creates Order record (without items)                          │
│    - Inserts into Orders table                                     │
└────────────────────────────────┬───────────────────────────────────┘
                                 │
                                 ▼
┌────────────────────────────────────────────────────────────────────┐
│ 3. Saga Orchestrator Starts                                         │
│    - Generates SagaId (GUID)                                       │
│    - Creates SagaState record                                      │
│    - Sets CurrentStep = "OrderCreated"                             │
│    - Inserts into SagaStates table                                 │
└────────────────────────────────┬───────────────────────────────────┘
                                 │
                                 ▼
┌────────────────────────────────────────────────────────────────────┐
│ 4. Publish OrderCreatedEvent                                        │
│    {                                                               │
│      "sagaId": "guid",                                             │
│      "orderId": 1,                                                 │
│      "customerId": "CUST-001",                                     │
│      "items": [...],                                               │
│      "totalAmount": 999                                            │
│    }                                                               │
└────────────────────────────────┬───────────────────────────────────┘
                                 │
                                 ▼
                           MESSAGE BUS
                                 │
                    ┌────────────┴────────────┐
                    │                         │
                    ▼                         ▼
        Order Details Service     Analytics/Logging Services
```

## Technology Stack

```
┌─────────────────────────────────────────────┐
│           Presentation Layer                 │
│  - ASP.NET Core Web API                     │
│  - Swagger/OpenAPI                          │
│  - Controllers                              │
└─────────────────┬───────────────────────────┘
                  │
┌─────────────────▼───────────────────────────┐
│          Application Layer                   │
│  - MediatR (CQRS)                           │
│  - Command Handlers                         │
│  - Query Handlers                           │
└─────────────────┬───────────────────────────┘
                  │
┌─────────────────▼───────────────────────────┐
│           Domain Layer                       │
│  - Entities (Order, SagaState)              │
│  - Domain Logic                             │
│  - Status Enums                             │
└─────────────────┬───────────────────────────┘
                  │
┌─────────────────▼───────────────────────────┐
│        Infrastructure Layer                  │
│  - Entity Framework Core                    │
│  - SQLite (Development)                     │
│  - SQL Server (Production)                  │
└─────────────────┬───────────────────────────┘
                  │
┌─────────────────▼───────────────────────────┐
│          Messaging Layer                     │
│  - MassTransit                              │
│  - InMemory (Development)                   │
│  - RabbitMQ (Production)                    │
│  - Azure Service Bus (Cloud)                │
└─────────────────────────────────────────────┘
```

## Project Structure

```
OrderService/
│
├── Domain/                        # Core business entities
│   ├── Order.cs
│   ├── OrderStatus.cs
│   └── SagaState.cs
│
├── Infrastructure/                # Data access
│   └── Database/
│       └── OrderDbContext.cs
│
├── Features/                      # Use cases (CQRS)
│   ├── CreateOrder/
│   │   ├── CreateOrderController.cs
│   │   └── CreateOrderCommandHandler.cs
│   └── GetOrderStatus/
│       └── GetOrderStatusQueryHandler.cs
│
├── Saga/                          # Saga orchestration
│   └── OrderSagaOrchestrator.cs
│
├── Consumers/                     # Event consumers
│   ├── OrderDetailsCompletedConsumer.cs
│   ├── PaymentCompletedConsumer.cs
│   └── NotificationCompletedConsumer.cs
│
├── Messages/                      # Message contracts
│   ├── Events/
│   │   ├── OrderCreatedEvent.cs
│   │   ├── OrderDetailsCompletedEvent.cs
│   │   ├── PaymentCompletedEvent.cs
│   │   ├── NotificationCompletedEvent.cs
│   │   ├── OrderCancelledEvent.cs
│   │   └── OrderCompletedEvent.cs
│   └── Commands/
│       ├── ProcessPaymentCommand.cs
│       └── SendNotificationCommand.cs
│
├── DTOs/                          # Data transfer objects
│   ├── CreateOrderRequest.cs
│   ├── CreateOrderResponse.cs
│   └── OrderStatusResponse.cs
│
├── Program.cs                     # Application entry point
├── appsettings.json              # Configuration
└── OrderService.csproj           # Project file
```

## Deployment Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                       LOAD BALANCER                          │
└────────────────┬───────────────────────┬────────────────────┘
                 │                       │
        ┌────────▼────────┐     ┌───────▼─────────┐
        │ Order Service   │     │ Order Service   │
        │   Instance 1    │     │   Instance 2    │
        └────────┬────────┘     └───────┬─────────┘
                 │                       │
        ┌────────▼───────────────────────▼────────┐
        │         MESSAGE BUS (RabbitMQ)           │
        │         - Durable Queues                 │
        │         - Message Persistence            │
        └────────┬───────────────────────┬─────────┘
                 │                       │
        ┌────────▼────────┐     ┌───────▼─────────┐
        │  Order Details  │     │  Payment Service│
        │    Service      │     │                 │
        └─────────────────┘     └─────────────────┘
                 │
        ┌────────▼────────┐
        │  SQL Database   │
        │  (Orders)       │
        └─────────────────┘
```

## Key Patterns Used

1. **Saga Pattern** - Distributed transaction management
2. **CQRS** - Command Query Responsibility Segregation
3. **Event-Driven Architecture** - Loose coupling via events
4. **Repository Pattern** - Data access abstraction
5. **Dependency Injection** - IoC container
6. **Mediator Pattern** - Decoupled request handling
7. **Compensation Pattern** - Rollback handling
8. **Retry Pattern** - Fault tolerance

---

This architecture provides:
✅ Scalability
✅ Resilience
✅ Loose Coupling
✅ Testability
✅ Maintainability
✅ Production-Ready
