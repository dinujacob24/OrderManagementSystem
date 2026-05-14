# Console Log Output Reference

## What You Should See When Running OrderDetailsService

### **On Startup (Immediate):**
```
info: OrderDetailsService.Background.OutboxConsumer[0]
      OutboxConsumer started - polling OrderService outbox for OrderCreatedEvent
info: OrderDetailsService.Background.OutboxConsumer[0]
      OutboxConsumer: Polling interval = 5 seconds, Max attempts = 5
```

---

### **Every 5 Seconds (If Unprocessed Messages Exist):**
```
info: OrderDetailsService.Background.OutboxConsumer[0]
      OutboxConsumer: Polling... Found 1 unprocessed OrderCreatedEvent messages
info: OrderDetailsService.Background.OutboxConsumer[0]
      OutboxConsumer: Found 1 OrderCreatedEvent messages to process
info: OrderDetailsService.Background.OutboxConsumer[0]
      OutboxConsumer: Processing OrderCreatedEvent a1b2c3d4-..., Attempt 1
```

---

### **When Processing Order Items (Success):**
```
info: OrderDetailsService.Consumers.OrderCreatedConsumer[0]
      OrderDetailsService: Processing OrderCreated for OrderId: 18, SagaId: e6e71a82-...
info: OrderDetailsService.Consumers.OrderCreatedConsumer[0]
      OrderDetailsService: Successfully added 2 items for OrderId: 18
info: OrderDetailsService.Background.OutboxConsumer[0]
      OutboxConsumer: Successfully processed OrderCreatedEvent a1b2c3d4-...
```

---

### **If Nothing to Process (Silent):**
No logs appear - just waits 5 seconds and polls again.

---

## What You Should See When Running OrderService

### **On Startup (Immediate):**
```
info: OrderService.Background.OutboxConsumer[0]
      OutboxConsumer started - polling OrderDetailsService outbox
info: OrderService.Background.OutboxConsumer[0]
      OutboxConsumer: Polling interval = 5 seconds, Max attempts = 5
info: OrderService.Saga.OrderSagaOrchestrator[0]
      Saga e6e71a82-... started for Order 18
info: OrderService.Saga.OrderSagaOrchestrator[0]
      OrderCreatedEvent saved to outbox for Saga e6e71a82-...
```

---

### **When OrderDetailsCompletedEvent Arrives:**
```
info: OrderService.Background.OutboxConsumer[0]
      OutboxConsumer: Polling... Found 1 unprocessed OrderDetails event messages
info: OrderService.Background.OutboxConsumer[0]
      OutboxConsumer: Processing message b2c3d4e5-... of type OrderDetailsCompletedEvent, Attempt 1
info: OrderService.Saga.OrderSagaOrchestrator[0]
      Order details completed for Saga e6e71a82-..., initiating payment
info: OrderService.Consumers.MockPaymentConsumer[0]
      MockPaymentService: Processing payment for Order 18, Amount: 100.00
info: OrderService.Consumers.MockPaymentConsumer[0]
      MockPaymentService: Payment completed for Order 18, TransactionId: xyz123...
info: OrderService.Consumers.PaymentCompletedConsumer[0]
      Received PaymentCompletedEvent for Saga e6e71a82-..., Order 18, Success: True
info: OrderService.Saga.OrderSagaOrchestrator[0]
      Payment completed for Saga e6e71a82-..., sending notification
info: OrderService.Consumers.MockNotificationConsumer[0]
      MockNotificationService: Sending OrderConfirmation for Order 18
info: OrderService.Consumers.MockNotificationConsumer[0]
      MockNotificationService: Message - Your order #18 has been successfully placed!
info: OrderService.Consumers.MockNotificationConsumer[0]
      MockNotificationService: Notification sent successfully for Order 18
info: OrderService.Consumers.NotificationCompletedConsumer[0]
      Received NotificationCompletedEvent for Saga e6e71a82-..., Order 18, Success: True
info: OrderService.Saga.OrderSagaOrchestrator[0]
      Saga e6e71a82-... completed successfully
```

---

## Troubleshooting: What's Missing?

### ❌ If you DON'T see "OutboxConsumer started"
**Problem:** Background service not starting
**Check:** 
- Service registration in `Program.cs`
- Startup errors in console
- Database connection issues

### ❌ If you see "started" but no polling logs
**Problem:** No unprocessed messages found
**Action:**
1. Create a new order
2. Check database: `Invoke-RestMethod -Uri "http://localhost:5001/api/DatabaseCheck/status" -Method GET`
3. Verify MessageType matches exactly: `"OrderCreatedEvent"`

### ❌ If you see polling but not "Processing"
**Problem:** Messages found but not claimed
**Possible reasons:**
- Messages have `Attempts >= 5`
- Messages locked by another process
- Query filter mismatch

### ❌ If you see "Processing" but not "Successfully added"
**Problem:** Exception during processing
**Check:** Look for error logs immediately after "Processing"
- Database constraint violations
- Serialization errors
- Null reference exceptions

---

## How to Test Right Now

### **Step 1: Restart OrderDetailsService**
```powershell
# Stop current instance (Ctrl+C in VS terminal)
cd C:\Users\2493103\source\repos\CaseStudy\OrderDetailsService
dotnet run
```

### **Step 2: Watch the Console**
Within 2 seconds you should see:
```
OutboxConsumer started - polling OrderService outbox for OrderCreatedEvent
OutboxConsumer: Polling interval = 5 seconds, Max attempts = 5
```

If you see this ✅ → OutboxConsumer is running!

### **Step 3: Create a Test Order**
In another PowerShell window:
```powershell
$body = @{
    customerId = "TEST-$(Get-Random)"
    items = @(@{
        productId = "PROD-001"
        productName = "Test Product"
        quantity = 1
        unitPrice = 50.00
    })
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/api/Orders" -Method POST -Body $body -ContentType "application/json"
```

### **Step 4: Watch OrderDetailsService Console**
Within 5-10 seconds you should see:
```
OutboxConsumer: Polling... Found 1 unprocessed OrderCreatedEvent messages
OutboxConsumer: Found 1 OrderCreatedEvent messages to process
OutboxConsumer: Processing OrderCreatedEvent...
OrderDetailsService: Processing OrderCreated for OrderId...
OrderDetailsService: Successfully added 1 items for OrderId...
OutboxConsumer: Successfully processed OrderCreatedEvent...
```

---

## Quick Diagnostic Command

Run this in PowerShell to see current state:
```powershell
# Check OrderDetailsService database status
$status = Invoke-RestMethod -Uri "http://localhost:5001/api/DatabaseCheck/status" -Method GET
$status | ConvertTo-Json -Depth 5

# Look for:
# - connectionString: Should be C:\Users\2493103\source\repos\CaseStudy\orderdetails.db
# - summary: Shows message counts by type
# - unprocessedMessages: Shows stuck messages
```

---

## Expected Timeline

```
T+0s:  Create order via API
T+0s:  OrderService saves OrderCreatedEvent to outbox
T+5s:  OrderDetailsService picks up event (next poll cycle)
T+5s:  OrderDetailsService creates order items
T+5s:  OrderDetailsService saves OrderDetailsCompletedEvent to outbox
T+10s: OrderService picks up completion event (next poll cycle)
T+10s: MockPaymentConsumer processes (instant)
T+10s: MockNotificationConsumer processes (instant)
T+10s: Order status changes to "Completed"
```

**Total time: ~10-15 seconds from order creation to completion**

If it takes longer, check the console for:
- Error messages
- Retry attempts (Attempt 2, Attempt 3, etc.)
- "exceeded max attempts" messages
