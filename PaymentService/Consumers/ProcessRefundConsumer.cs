namespace PaymentService.Consumers;

using Shared.Messages.Commands;
using Shared.Messages.Events;
using PaymentService.Infrastructure.Database;
using PaymentService.Domain;
using Microsoft.EntityFrameworkCore;

public class ProcessRefundConsumer
{
    private readonly PaymentDbContext _context;
    private readonly ILogger<ProcessRefundConsumer> _logger;

    public ProcessRefundConsumer(
        PaymentDbContext context,
        ILogger<ProcessRefundConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ConsumeAsync(ProcessRefundCommand command)
    {
        _logger.LogInformation("Processing refund for order {OrderId}", command.OrderId);

        try
        {
            // Find the original payment
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.OrderId == command.OrderId);

            if (payment == null)
            {
                _logger.LogWarning("Payment not found for order {OrderId}", command.OrderId);
                await PublishRefundFailedEvent(command, "Payment not found");
                return;
            }

            // Process refund logic here (mock or actual payment gateway integration)
            // For now, we'll just mark it as refunded

            var refund = new Refund
            {
                Id = Guid.NewGuid(),
                PaymentId = payment.PaymentId,
                OrderId = command.OrderId,
                Amount = command.Amount,
                Status = "Completed",
                ProcessedAt = DateTime.UtcNow
            };

            // Add refund to database
            _context.Refunds.Add(refund);

            // Publish RefundCompletedEvent
            var refundCompletedEvent = new RefundCompletedEvent
            {
                OrderId = command.OrderId,
                PaymentId = payment.PaymentId,
                RefundAmount = command.Amount,
                Success = true,
                Message = "Refund processed successfully",
                CorrelationId = command.CorrelationId
            };

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = "refund-completed",
                Payload = System.Text.Json.JsonSerializer.Serialize(refundCompletedEvent),
                CreatedAt = DateTime.UtcNow
            };

            _context.OutboxMessages.Add(outboxMessage);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Refund processed successfully for order {OrderId}", command.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing refund for order {OrderId}", command.OrderId);
            await PublishRefundFailedEvent(command, ex.Message);
        }
    }

    private async Task PublishRefundFailedEvent(ProcessRefundCommand command, string errorMessage)
    {
        var refundCompletedEvent = new RefundCompletedEvent
        {
            OrderId = command.OrderId,
            PaymentId = command.PaymentId,
            RefundAmount = command.Amount,
            Success = false,
            Message = errorMessage,
            CorrelationId = command.CorrelationId
        };

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "refund-completed",
            Payload = System.Text.Json.JsonSerializer.Serialize(refundCompletedEvent),
            CreatedAt = DateTime.UtcNow
        };

        _context.OutboxMessages.Add(outboxMessage);
        await _context.SaveChangesAsync();
    }
}
