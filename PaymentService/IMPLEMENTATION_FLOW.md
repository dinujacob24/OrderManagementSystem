# PaymentService Complete Flow Explanation

## 🎯 Overview

PaymentService is integrated into the saga orchestration using the **Database Outbox Pattern** for reliable, asynchronous cross-service communication via a **shared SQLite database**.

---

## 📊 Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                    Shared SQLite Database                           │
│              orderdetails.db                                        │
│                                                                     │
│  ┌───────────────┐  ┌──────────────┐  ┌──────────────────────┐   │
│  │   Orders      │  │  SagaStates  │  │   OutboxMessages     │   │
│  │   OrderItems  │  │   Payments   │  │   (Shared Queue)     │   │
│  └───────────────┘  └──────────────┘  └──────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
         ↑                    ↑                       ↑
         │                    │                       │
    ┌────┴────┐         ┌─────┴─────┐         ┌──────┴──────┐
    │ Order   │         │  Order    │         │   Payment   │
    │ Details │         │  Service  │         │   Service   │
    │ Service │         │  (Saga)   │         │             │
    └─────────┘         └───────────┘         └─────────────┘
```

---

## 🔄 Complete Flow (Step-by-Step)

### **🟢 Phase 1: Order Creation**

**Service**: OrderService  
**File**: `OrderService/Saga/OrderSagaOrchestrator.cs` → `StartOrderSaga()`

```
1. Customer creates order via POST /api/Orders
   ↓
2. OrderService creates:
   - Order record (Status = "OrderDetailsProcessing")
   - SagaState record (CurrentStep = "OrderCreated")
   ↓
3. Writes OutboxMessage:
   {
     MessageType: "OrderCreatedEvent",
     Payload: { SagaId, OrderId, CustomerId, Items, TotalAmount },
     Processed: false
   }
   ↓
4. SaveChangesAsync() commits all to database
```

---

### **🟠 Phase 2: Order Details Processing**

**Service**: OrderDetailsService  
**File**: `OrderDetailsService/Background/OutboxConsumer.cs`

```
Every 5 seconds, OutboxConsumer polls:

1. SELECT * FROM OutboxMessages 
   WHERE MessageType = 'OrderCreatedEvent' 
   AND Processed = 0
   ↓
2. Claims message with LockToken (prevents duplicates)
   ↓
3. Deserializes OrderCreatedEvent
   ↓
4. Creates OrderItems in database
   ↓
5. Writes OutboxMessage:
   {
     MessageType: "OrderDetailsCompletedEvent",
     Payload: { SagaId, OrderId, Items, Success: true },
     Processed: false
   }
   ↓
6. Marks OrderCreatedEvent as Processed = true
   ↓
7. SaveChangesAsync()
```

---

### **🔵 Phase 3: Order Details Completion (Saga Update)**

**Service**: OrderService  
**File**: `OrderService/Background/OutboxConsumer.cs` → calls `OrderSagaOrchestrator.HandleOrderDetailsCompleted()`

```
Every 5 seconds, OutboxConsumer polls:

1. SELECT * FROM OutboxMessages 
   WHERE MessageType = 'OrderDetailsCompletedEvent' 
   AND Processed = 0
   ↓
2. Claims and deserializes event
   ↓
3. Calls OrderSagaOrchestrator.HandleOrderDetailsCompleted()
   ↓
4. Updates SagaState:
   - IsOrderDetailsCompleted = true
   - CurrentStep = "OrderDetailsCompleted"
   ↓
5. Updates Order:
   - Status = "PaymentProcessing"
   ↓
6. Creates ProcessPaymentCommand
   ↓
7. Writes OutboxMessage:
   {
     MessageType: "ProcessPaymentCommand",
     Payload: { SagaId, OrderId, CustomerId, Amount },
     Processed: false
   }
   ↓
8. Marks OrderDetailsCompletedEvent as Processed = true
   ↓
9. SaveChangesAsync() ✅
```

---

### **💳 Phase 4: Payment Processing**

**Service**: PaymentService  
**File**: `PaymentService/Background/OutboxConsumer.cs`

```
Every 5 seconds, OutboxConsumer polls:

1. SELECT * FROM OutboxMessages 
   WHERE MessageType = 'ProcessPaymentCommand' 
   AND Processed = 0
   ↓
2. Claims and deserializes command
   ↓
3. Creates Payment record:
   {
     SagaId, OrderId, CustomerId, Amount,
     Status: "Processing",
     CreatedAt: now
   }
   ↓
4. Calls SimulatePaymentProcessing():

   RULES:
   ✅ SUCCESS (default)
   ❌ FAIL if amount ends in .99 → "Insufficient funds"
   ❌ FAIL if customerId contains "fail" → "Card declined"
   ❌ FAIL if amount > 10000 → "Exceeds limit"
   ❌ FAIL 10% random → "Gateway timeout"
   ↓
5. Updates Payment record:
   - Status = "Completed" or "Failed"
   - TransactionId = "TXN-XXXXXXXX" (if success)
   - ErrorMessage = error message (if failed)
   - ProcessedAt = now
   ↓
6. Writes OutboxMessage:
   {
     MessageType: "PaymentCompletedEvent",
     Payload: { 
       SagaId, OrderId, Amount, 
       Success: true/false, 
       TransactionId, 
       ErrorMessage 
     },
     Processed: false
   }
   ↓
7. Marks ProcessPaymentCommand as Processed = true
   ↓
8. SaveChangesAsync() ✅
```

---

### **🟢 Phase 5: Payment Completion (Saga Update)**

**Service**: OrderService  
**File**: `OrderService/Background/OutboxConsumer.cs` → calls `OrderSagaOrchestrator.HandlePaymentCompleted()`

```
OutboxConsumer polls for PaymentCompletedEvent:

1. SELECT * FROM OutboxMessages 
   WHERE MessageType = 'PaymentCompletedEvent' 
   AND Processed = 0
   ↓
2. Deserializes PaymentCompletedEvent
   ↓
3. Calls OrderSagaOrchestrator.HandlePaymentCompleted(event)
   ↓
4. IF event.Success == true:

   a) Updates SagaState:
      - IsPaymentCompleted = true
      - CurrentStep = "PaymentCompleted"
      ↓
   b) Updates Order:
      - Status = "NotificationProcessing"
      ↓
   c) Writes SendNotificationCommand to outbox
      (for NotificationService to pick up)
      ↓
   d) SaveChangesAsync() ✅

5. IF event.Success == false:

   a) Calls CompensatePayment()
      ↓
   b) Updates SagaState:
      - CurrentStep = "Failed"
      - ErrorMessage = payment error
      ↓
   c) Updates Order:
      - Status = "Cancelled"
      ↓
   d) Triggers compensation logic:
      - May reverse order details
      - Sends cancellation notification
      ↓
   e) SaveChangesAsync() ✅

6. Marks PaymentCompletedEvent as Processed = true
   ↓
7. SaveChangesAsync() ✅
```

---

## 🔍 Key Implementation Details

### **1. Database Outbox Pattern**

**Why?**
- Ensures **atomic writes** (message + data in same transaction)
- **At-least-once delivery** guarantee
- **No message loss** if service crashes
- **No dependency** on external message brokers

**How?**
```sql
-- Atomic write
BEGIN TRANSACTION;
  INSERT INTO Payments (...);
  INSERT INTO OutboxMessages (...);
COMMIT;

-- Separate polling process marks as processed
UPDATE OutboxMessages SET Processed = 1 WHERE Id = ?;
```

### **2. Lock Token Mechanism**

Prevents duplicate processing in concurrent scenarios:

```sql
UPDATE OutboxMessages 
SET LockToken = 'unique-guid', 
    LockExpiresAt = 'now + 5 minutes'
WHERE Id IN (
  SELECT Id FROM OutboxMessages 
  WHERE Processed = 0 
  AND (LockExpiresAt IS NULL OR LockExpiresAt < NOW())
  LIMIT 20
)
```

**Benefits:**
- Only one consumer processes each message
- Locks expire if service crashes
- Automatic retry on failure

### **3. Payment Simulation Logic**

**File**: `PaymentService/Background/OutboxConsumer.cs` → `SimulatePaymentProcessing()`

```csharp
private (bool success, string? errorMessage) SimulatePaymentProcessing(command)
{
    // Rule 1: Insufficient funds
    if (command.Amount % 1 == 0.99m)
        return (false, "Insufficient funds");

    // Rule 2: Card declined
    if (command.CustomerId.Contains("fail", StringComparison.OrdinalIgnoreCase))
        return (false, "Payment card declined");

    // Rule 3: Exceeds limit
    if (command.Amount > 10000)
        return (false, "Transaction amount exceeds limit");

    // Rule 4: Random failures (10%)
    if (Random.Shared.Next(100) < 10)
        return (false, "Payment gateway timeout");

    // Default: Success
    return (true, null);
}
```

---

## 🧪 Testing Scenarios

### **✅ Successful Payment**
```json
POST /api/Orders
{
  "customerId": "customer123",
  "totalAmount": 100.00,
  "items": [{ "productId": "P1", "quantity": 2, "unitPrice": 50 }]
}
```
**Expected**:
- Order → OrderDetailsProcessing → PaymentProcessing → NotificationProcessing → Completed
- Payment record with Status="Completed", TransactionId="TXN-XXXXXXXX"

### **❌ Payment Fails (Insufficient Funds)**
```json
{
  "customerId": "customer123",
  "totalAmount": 99.99
}
```
**Expected**:
- Payment fails with "Insufficient funds"
- Order Status = "Cancelled"
- SagaState.ErrorMessage = "Payment processing failed: Insufficient funds"

### **❌ Payment Fails (Card Declined)**
```json
{
  "customerId": "customer-fail-001",
  "totalAmount": 100.00
}
```
**Expected**:
- Payment fails with "Payment card declined"
- Order cancelled with compensation

---

## 📊 Database Schema

### **Payments Table**
```sql
CREATE TABLE Payments (
    PaymentId INTEGER PRIMARY KEY AUTOINCREMENT,
    SagaId TEXT NOT NULL,
    OrderId INTEGER NOT NULL,
    CustomerId TEXT NOT NULL,
    Amount REAL NOT NULL,
    Status TEXT NOT NULL,  -- Pending, Processing, Completed, Failed
    PaymentMethod TEXT,
    TransactionId TEXT,
    ErrorMessage TEXT,
    CreatedAt TEXT NOT NULL,
    ProcessedAt TEXT
);
```

### **OutboxMessages Table** (Shared)
```sql
CREATE TABLE OutboxMessages (
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
```

---

## 🚀 Running the Services

1. **Start all services**:
   - OrderService (port 5001)
   - OrderDetailsService (port 5002)
   - PaymentService (port 5003)

2. **Verify shared database**: `C:\Users\2493103\source\repos\CaseStudy\orderdetails.db`

3. **Create an order**:
```bash
curl -X POST http://localhost:5001/api/Orders \
  -H "Content-Type: application/json" \
  -d '{"customerId": "C123", "totalAmount": 100, "items": [...]}'
```

4. **Monitor logs** for each service showing polling and processing

5. **Check diagnostics**:
```bash
# OrderService
GET http://localhost:5001/api/Diagnostics/saga/{orderId}

# PaymentService
GET http://localhost:5003/api/Diagnostics/payments/{orderId}
```

---

## ✅ Summary

PaymentService integrates seamlessly into the saga pattern by:

1. **Polling** the shared outbox for `ProcessPaymentCommand`
2. **Processing** payments with configurable success/failure logic
3. **Publishing** `PaymentCompletedEvent` back to the outbox
4. **Enabling** OrderService to continue the saga or trigger compensation

All communication is **reliable, atomic, and doesn't require external message brokers**!
