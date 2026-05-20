using Shared.DTOs;

namespace Shared.Logging
{
    public interface ILogSender
    {
        /// <summary>
        /// Sends a manual log entry to the centralized logging service.
        /// </summary>
        Task SendLogAsync(LogEntryDto log);

        /// <summary>
        /// Convenience method for quick logging without manually creating a DTO.
        /// </summary>
        Task SendLogAsync(string message, string logLevel = "Information", string? exception = null);
    }
}
