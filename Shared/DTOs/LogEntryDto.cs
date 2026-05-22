using System;

namespace Shared.DTOs
{
    public class LogEntryDto
    {
        public string ServiceName { get; set; } = string.Empty;
        public string? CorrelationId { get; set; }
        public string LogLevel { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Exception { get; set; }
        public string? UserName { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
