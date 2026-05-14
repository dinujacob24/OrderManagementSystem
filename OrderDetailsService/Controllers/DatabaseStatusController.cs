using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderDetailsService.Infrastructure.Database;

namespace OrderDetailsService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DatabaseStatusController : ControllerBase
    {
        private readonly OrderDetailsDbContext _context;

        public DatabaseStatusController(OrderDetailsDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                var connectionString = _context.Database.GetConnectionString();

                // Try to query each table
                var itemsCount = await _context.OrderItems.CountAsync();
                var outboxCount = await _context.OutboxMessages.CountAsync();

                return Ok(new
                {
                    connectionString,
                    status = "Connected",
                    tables = new
                    {
                        orderItems = itemsCount,
                        outboxMessages = outboxCount
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    status = "Error",
                    error = ex.Message,
                    innerError = ex.InnerException?.Message
                });
            }
        }

        [HttpPost("recreate")]
        public async Task<IActionResult> RecreateDatabase()
        {
            try
            {
                _context.Database.EnsureDeleted();
                _context.Database.EnsureCreated();

                return Ok(new { message = "Database recreated successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
