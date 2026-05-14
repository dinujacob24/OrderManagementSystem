# Payment Service

## Overview
PaymentService is a microservice responsible for processing payment transactions in the Order Management System. It follows the saga orchestration pattern and communicates via a shared database outbox pattern.

## Architecture

### Database Pattern
- **Shared SQLite Database**: `C:\Users\2493103\source\repos\CaseStudy\orderdetails.db`
- **Tables**:
  - `Payments`: Stores payment records
  - `OutboxMessages`: Shared table for event-driven communication

### Communication Flow
1. **OrderService** writes `ProcessPaymentCommand` to outbox after order details complete
2. **PaymentService** polls outbox for `ProcessPaymentCommand` messages
3. **PaymentService** processes payment and writes `PaymentCompletedEvent` to outbox
4. **OrderService** polls outbox for `PaymentCompletedEvent` and updates saga state

## Payment Simulation Rules

The service simulates different payment scenarios without requiring a real payment gateway:

### Success Scenarios
- ✅ **Default**: Most payments succeed
- ✅ **Normal amounts**: Any amount under $10,000

### Failure Scenarios
Payment fails automatically in these cases:

1. **Amount Rule**: Amounts ending in `.99` → ❌ "Insufficient funds"
   - Example: `$99.99`, `$199.99`

2. **Customer ID Rule**: CustomerID containing "fail" → ❌ "Payment card declined"
   - Example: `customer-fail-123`, `FAIL001`

3. **Amount Limit Rule**: Amounts over `$10,000` → ❌ "Transaction amount exceeds limit"
   - Example: `$10,001`

4. **Random Failures**: 10% random failure rate → ❌ "Payment gateway timeout"
   - Simulates real-world gateway issues

## Testing Different Scenarios

### Test Successful Payment
```http
POST /api/Orders
{
  "customerId": "customer123",
  "totalAmount": 100.00,
  "items": [...]
}
```
Expected: Payment succeeds, order moves to `NotificationProcessing`

### Test Insufficient Funds
```http
POST /api/Orders
{
  "customerId": "customer123",
  "totalAmount": 99.99,
  "items": [...]
}
```
Expected: Payment fails with "Insufficient funds"

### Test Card Declined
```http
POST /api/Orders
{
  "customerId": "customer-fail-001",
  "totalAmount": 100.00,
  "items": [...]
}
```
Expected: Payment fails with "Payment card declined"

### Test Amount Limit
```http
POST /api/Orders
{
  "customerId": "customer123",
  "totalAmount": 15000.00,
  "items": [...]
}
```
Expected: Payment fails with "Transaction amount exceeds limit"

## API Endpoints

### Diagnostics

#### Get All Payments
```http
GET /api/Diagnostics/payments
```
Returns the 50 most recent payments

#### Get Payment by Order ID
```http
GET /api/Diagnostics/payments/{orderId}
```
Returns payment details for a specific order

#### Get Outbox Messages
```http
GET /api/Diagnostics/outbox
```
Returns payment-related outbox messages

## Domain Models

### Payment
```csharp
{
  "paymentId": 1,
  "sagaId": "guid",
  "orderId": 123,
  "customerId": "customer123",
  "amount": 100.00,
  "status": "Completed",  // Pending, Processing, Completed, Failed
  "paymentMethod": "SimulatedPayment",
  "transactionId": "TXN-ABCD1234",
  "errorMessage": null,
  "createdAt": "2026-01-01T10:00:00Z",
  "processedAt": "2026-01-01T10:00:05Z"
}
```

## Configuration

### appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=C:\\Users\\2493103\\source\\repos\\CaseStudy\\orderdetails.db"
  }
}
```

### Polling Configuration
- **Interval**: 5 seconds
- **Max Attempts**: 5
- **Lock Duration**: 5 minutes

## Compensation Logic

When a payment fails:
1. Payment status is set to `"Failed"`
2. Error message is recorded
3. `PaymentCompletedEvent` is published with `Success = false`
4. OrderService receives the event and triggers compensation:
   - Marks saga as failed
   - Sets order status to `Cancelled`
   - May trigger rollback of order details
   - Sends cancellation notification to customer

## Running the Service

### Prerequisites
- .NET 10 SDK
- SQLite database at specified path

### Start the Service
```bash
cd PaymentService
dotnet run
```

Default port: Check `launchSettings.json`

### Verify Service is Running
```bash
curl http://localhost:<port>/api/Diagnostics/payments
```

## Logs

The service logs important events:
- Payment command received
- Payment processing started
- Payment succeeded/failed
- Outbox message created
- Polling activity

Example logs:
```
PaymentService: Found 1 unprocessed payment commands
PaymentService: Processing payment for OrderId: 123, Amount: 100.00
PaymentService: Payment Completed for OrderId: 123, TransactionId: TXN-ABCD1234
```

## Future Enhancements

Potential improvements:
- Add real payment gateway integration (Stripe, PayPal, etc.)
- Implement payment method selection
- Add refund/chargeback support
- Implement payment status webhooks
- Add fraud detection rules
- Support multiple currencies
- Add payment retry logic for transient failures
