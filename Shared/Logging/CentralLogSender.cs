using Microsoft.AspNetCore.Http;
using Shared.DTOs;
using System.Net.Http.Json;
using Shared.Utilities;

namespace Shared.Logging
{
    public class CentralLogSender : ILogSender
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly string _serviceName;

        public CentralLogSender(HttpClient httpClient, IHttpContextAccessor httpContextAccessor, string serviceName)
        {
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
            _serviceName = serviceName;
        }

        public Task SendLogAsync(LogEntryDto log)
        {
            // Auto-populate if missing or empty
            if (string.IsNullOrWhiteSpace(log.ServiceName)) 
                log.ServiceName = _serviceName;

            log.Timestamp = log.Timestamp == default 
                ? TimeHelper.GetIstNow() 
                : log.Timestamp;

            // Use existing correlation ID or create a new one for this request
            log.CorrelationId ??= _httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-Id"].ToString();
            if (string.IsNullOrWhiteSpace(log.CorrelationId))
                log.CorrelationId = _httpContextAccessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();

            log.UserName ??= _httpContextAccessor.HttpContext?.User?.Identity?.Name 
                ?? _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;

            var originalMessage = log.Message;

            // 1. Send via HTTP
            try
            {
                _ = Task.Run(async () => {
                    try {
                        var httpLog = CloneLog(log);
                        httpLog.Message = "HTTP: " + originalMessage;

                        var response = await _httpClient.PostAsJsonAsync("logs", httpLog);
                        if (!response.IsSuccessStatusCode) {
                             Console.WriteLine($"[LOG ERROR] HTTP Logging failed for: {httpLog.Message}");
                        }
                    } catch { }
                });
            } catch { }

            return Task.CompletedTask;
        }

        private LogEntryDto CloneLog(LogEntryDto source)
        {
            return new LogEntryDto
            {
                ServiceName = source.ServiceName,
                CorrelationId = source.CorrelationId,
                LogLevel = source.LogLevel,
                Message = source.Message,
                Exception = source.Exception,
                UserName = source.UserName,
                Timestamp = source.Timestamp
            };
        }

        public Task SendLogAsync(string message, string logLevel = "Information", string? exception = null)
        {
            var log = new LogEntryDto
            {
                Message = message,
                LogLevel = logLevel,
                Exception = exception
            };
            return SendLogAsync(log);
        }
    }
}
