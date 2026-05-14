using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Infrastructure.Database;

namespace OrderService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DiagnosticsController : ControllerBase
    {
        private readonly OrderDbContext _context;

        public DiagnosticsController(OrderDbContext context)
        {
            _context = context;
        }

        [HttpGet("outbox")]
        public async Task<IActionResult> GetOutboxMessages()
        {
            var all = await _context.OutboxMessages
                .OrderByDescending(m => m.CreatedAt)
                .Take(50)
                .Select(m => new
                {
                    m.Id,
                    m.MessageType,
                    m.Processed,
                    m.Attempts,
                    m.CreatedAt,
                    m.ProcessedAt,
                    m.LastError,
                    m.Payload,
                    PayloadLength = m.Payload.Length
                })
                .ToListAsync();

            var messageTypeBreakdown = await _context.OutboxMessages
                .GroupBy(m => new { m.MessageType, m.Processed })
                .Select(g => new { 
                    MessageType = g.Key.MessageType, 
                    Processed = g.Key.Processed, 
                    Count = g.Count() 
                })
                .ToListAsync();

            var unprocessedCount = await _context.OutboxMessages
                .Where(m => !m.Processed)
                .CountAsync();

            return Ok(new
            {
                totalMessages = all.Count,
                unprocessedCount,
                messageTypeBreakdown,
                recentMessages = all,
                connectionString = _context.Database.GetConnectionString()
            });
        }

        [HttpGet("saga/{orderId}")]
        public async Task<IActionResult> GetSagaState(int orderId)
        {
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            var sagaState = await _context.SagaStates
                .FirstOrDefaultAsync(s => s.OrderId == orderId);

            var outboxMessages = await _context.OutboxMessages
                .Where(m => m.Payload.Contains($"\"OrderId\":{orderId}"))
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    m.Id,
                    m.MessageType,
                    m.Processed,
                    m.CreatedAt,
                    m.ProcessedAt,
                    m.Attempts,
                    m.LastError,
                    m.LockToken,
                    m.LockExpiresAt,
                    PayloadPreview = m.Payload.Length > 200 ? m.Payload.Substring(0, 200) + "..." : m.Payload
                })
                .ToListAsync();

            return Ok(new
            {
                order = order != null ? new
                {
                    order.OrderId,
                    order.CustomerId,
                    order.Status,
                    order.TotalAmount,
                    order.OrderDate
                } : null,
                sagaState = sagaState != null ? new
                {
                    sagaState.SagaId,
                    sagaState.OrderId,
                    sagaState.CurrentStep,
                    sagaState.IsOrderDetailsCompleted,
                    sagaState.IsPaymentCompleted,
                    sagaState.IsNotificationCompleted,
                    sagaState.StartedAt,
                    sagaState.CompletedAt,
                    sagaState.ErrorMessage,
                    sagaState.RetryCount
                } : null,
                outboxMessages,
                timestamp = DateTime.UtcNow
            });
        }

        [HttpPost("test-saga-update/{orderId}")]
        public async Task<IActionResult> TestSagaUpdate(int orderId)
        {
            var sagaState = await _context.SagaStates
                .FirstOrDefaultAsync(s => s.OrderId == orderId);

            if (sagaState == null)
            {
                return NotFound("Saga state not found");
            }

            // Manually update to test if SaveChanges works
            sagaState.IsOrderDetailsCompleted = true;
            sagaState.CurrentStep = "ManuallyUpdated";

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Saga state manually updated",
                sagaId = sagaState.SagaId,
                orderId = sagaState.OrderId,
                isOrderDetailsCompleted = sagaState.IsOrderDetailsCompleted,
                currentStep = sagaState.CurrentStep
            });
        }
    }
}
