using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Infrastructure.Database;
using Shared.Messages;

namespace NotificationService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DiagnosticsController : ControllerBase
    {
        private readonly NotificationDbContext _db;
        private readonly ILogger<DiagnosticsController> _logger;

        public DiagnosticsController(NotificationDbContext db, ILogger<DiagnosticsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var totalNotifications = await _db.Notifications.CountAsync();
            var sentNotifications = await _db.Notifications.CountAsync(n => n.Status == "Sent");
            var failedNotifications = await _db.Notifications.CountAsync(n => n.Status == "Failed");
            var pendingCommands = await _db.OutboxMessages
                .CountAsync(m => m.MessageType == MessageTypes.SendNotificationCommand && !m.Processed);
            var pendingEvents = await _db.OutboxMessages
                .CountAsync(m => m.MessageType == nameof(Shared.Messages.Events.NotificationCompletedEvent) && !m.Processed);

            return Ok(new
            {
                totalNotifications,
                sentNotifications,
                failedNotifications,
                pendingCommands,
                pendingEvents
            });
        }

        [HttpGet("notifications")]
        public async Task<IActionResult> GetNotifications([FromQuery] int? orderId = null)
        {
            var query = _db.Notifications.AsQueryable();
            if (orderId.HasValue)
            {
                query = query.Where(n => n.OrderId == orderId.Value);
            }

            var notifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .ToListAsync();

            return Ok(notifications);
        }

        [HttpGet("outbox")]
        public async Task<IActionResult> GetOutbox([FromQuery] string? messageType = null)
        {
            var query = _db.OutboxMessages.AsQueryable();
            if (!string.IsNullOrEmpty(messageType))
            {
                query = query.Where(o => o.MessageType == messageType);
            }

            var messages = await query
                .OrderByDescending(o => o.CreatedAt)
                .Take(50)
                .Select(o => new
                {
                    o.Id,
                    o.MessageType,
                    o.Processed,
                    o.CreatedAt,
                    o.ProcessedAt,
                    o.Attempts,
                    o.LastError
                })
                .ToListAsync();

            return Ok(messages);
        }

        [HttpGet("notification/{id}")]
        public async Task<IActionResult> GetNotification(int id)
        {
            var notification = await _db.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == id);

            if (notification == null)
            {
                return NotFound();
            }

            return Ok(notification);
        }
    }
}
