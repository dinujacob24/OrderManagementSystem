using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderDetailsService.Infrastructure.Database;

namespace OrderDetailsService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DiagnosticsController : ControllerBase
    {
        private readonly OrderDetailsDbContext _context;

        public DiagnosticsController(OrderDetailsDbContext context)
        {
            _context = context;
        }

        [HttpGet("outbox")]
        public async Task<IActionResult> GetOutboxMessages()
        {
            var all = await _context.OutboxMessages
                .OrderByDescending(m => m.CreatedAt)
                .Take(20)
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

            var unprocessed = await _context.OutboxMessages
                .Where(m => !m.Processed && m.MessageType == "OrderCreatedEvent")
                .CountAsync();

            return Ok(new
            {
                totalMessages = all.Count,
                unprocessedOrderCreatedEvents = unprocessed,
                messages = all,
                connectionString = _context.Database.GetConnectionString()
            });
        }
    }
}
