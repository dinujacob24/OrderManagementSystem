using LoggingService.Data;
using LoggingService.Models;
using Mapster;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;

namespace LoggingService.Controllers
{
    [ApiController]
    [Route("logs")]
    public class LogsController : ControllerBase
    {
        private readonly LoggingDbContext _context;

        public LogsController(LoggingDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> CreateLog([FromBody] LogEntryDto logDto)
        {
            var logEntry = logDto.Adapt<LogEntry>();
            _context.Logs.Add(logEntry);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<LogEntryDto>>> GetLogs(
            [FromQuery] string? serviceName,
            [FromQuery] string? level,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var query = _context.Logs.AsQueryable();

            if (!string.IsNullOrEmpty(serviceName))
            {
                query = query.Where(l => l.ServiceName == serviceName);
            }

            if (!string.IsNullOrEmpty(level))
            {
                query = query.Where(l => l.LogLevel == level);
            }

            if (from.HasValue)
            {
                query = query.Where(l => l.Timestamp >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(l => l.Timestamp <= to.Value);
            }

            var logs = await query
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();

            var logDtos = logs.Adapt<IEnumerable<LogEntryDto>>();
            return Ok(logDtos);
        }
    }
}
