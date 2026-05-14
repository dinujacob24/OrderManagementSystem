using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentService.Infrastructure.Database;

namespace PaymentService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DiagnosticsController : ControllerBase
    {
        private readonly PaymentDbContext _context;

        public DiagnosticsController(PaymentDbContext context)
        {
            _context = context;
        }

        [HttpGet("payments")]
        public async Task<IActionResult> GetPayments()
        {
            var payments = await _context.Payments
                .OrderByDescending(p => p.CreatedAt)
                .Take(50)
                .ToListAsync();

            return Ok(payments);
        }

        [HttpGet("payments/{orderId}")]
        public async Task<IActionResult> GetPaymentByOrderId(int orderId)
        {
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.OrderId == orderId);

            if (payment == null)
            {
                return NotFound("Payment not found for this order");
            }

            return Ok(payment);
        }

        [HttpGet("outbox")]
        public async Task<IActionResult> GetOutboxMessages()
        {
            var messages = await _context.OutboxMessages
                .Where(m => m.MessageType == "ProcessPaymentCommand" || m.MessageType == "PaymentCompletedEvent")
                .OrderByDescending(m => m.CreatedAt)
                .Take(50)
                .Select(m => new
                {
                    m.Id,
                    m.MessageType,
                    m.Processed,
                    m.CreatedAt,
                    m.ProcessedAt,
                    m.Attempts,
                    m.LastError,
                    PayloadPreview = m.Payload.Length > 200 ? m.Payload.Substring(0, 200) + "..." : m.Payload
                })
                .ToListAsync();

            return Ok(new
            {
                totalMessages = messages.Count,
                messages
            });
        }
    }
}
