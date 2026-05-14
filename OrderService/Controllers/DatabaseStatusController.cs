using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Infrastructure.Database;

namespace OrderService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DatabaseStatusController : ControllerBase
    {
        private readonly OrderDbContext _context;

        public DatabaseStatusController(OrderDbContext context)
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
                var ordersCount = await _context.Orders.CountAsync();
                var sagasCount = await _context.SagaStates.CountAsync();
                var outboxCount = await _context.OutboxMessages.CountAsync();

                return Ok(new
                {
                    connectionString,
                    status = "Connected",
                    tables = new
                    {
                        orders = ordersCount,
                        sagaStates = sagasCount,
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
