using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.DTOs;
using System.Net.Http.Json;
using Shared.Utilities;

namespace Shared.Logging
{
    public class CentralLogger : ILogger
    {
        private static readonly HttpClient Http = new();
        private readonly string _category;
        private readonly IHttpContextAccessor _access;
        private readonly string _service;
        private readonly string _url;

        public CentralLogger(string category, IHttpContextAccessor access, string service, string url)
        {
            _category = category;
            _access = access;
            _service = service;
            _url = url;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        // Defer to the standard logging filter pipeline configured via
        // appsettings.json (Logging:LogLevel). The framework will not call Log()
        // for categories/levels that are filtered out.
        public bool IsEnabled(LogLevel level) => level != LogLevel.None;

        // Categories and message fragments that are pure infrastructure noise
        private static readonly string[] _noisyCategories =
        [
            "Microsoft.AspNetCore.StaticFiles",
            "Microsoft.AspNetCore.Hosting.Diagnostics",
            "Microsoft.AspNetCore.DataProtection",
            "MassTransit",
        ];

        private static readonly string[] _noisyMessageFragments =
        [
            "/swagger/",
            "swagger.json",
            "Endpoint Ready:",
            "Starting bus",
            "Bus started",
            "Configured endpoint",
            "DataProtection",
            "database schema",
            ".css",
            ".js",
        ];

        private bool IsNoise(LogLevel level, string category, string message)
        {
            if (level == LogLevel.Debug || level == LogLevel.Trace)
                return true;

            foreach (var c in _noisyCategories)
                if (category.StartsWith(c, StringComparison.OrdinalIgnoreCase))
                    return true;

            foreach (var f in _noisyMessageFragments)
                if (message.Contains(f, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? ex, Func<TState, Exception?, string> fmt)
        {
            if (!IsEnabled(level)) return;

            var rawMsg = fmt(state, ex);
            var fullMsg = $"[{_category}] {rawMsg}";

            if (IsNoise(level, _category, fullMsg)) return;

            var log = new LogEntryDto
            {
                ServiceName = _service,
                LogLevel = level.ToString(),
                Exception = ex?.ToString(),
                CorrelationId = _access.HttpContext?.Request.Headers["X-Correlation-Id"].ToString(),
                UserName = _access.HttpContext?.User?.Identity?.Name 
                    ?? _access.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? _access.HttpContext?.User?.FindFirst("sub")?.Value,
                Timestamp = TimeHelper.GetIstNow()
            };

            // HTTP Path
            _ = Task.Run(async () => {
                try {
                    var httpLog = CloneLog(log);
                    httpLog.Message = "HTTP: " + fullMsg;
                    await Http.PostAsJsonAsync($"{_url.TrimEnd('/')}/logs", httpLog);
                } catch { }
            });
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
    }

    public class CentralLoggerProvider : ILoggerProvider
    {
        private readonly IHttpContextAccessor _access;
        private readonly string _service;
        private readonly string _url;

        public CentralLoggerProvider(IHttpContextAccessor access, string service, string url)
        {
            _access = access;
            _service = service;
            _url = url;
        }

        public ILogger CreateLogger(string category) => new CentralLogger(category, _access, _service, _url);
        public void Dispose() { }
    }

    public static class CentralLoggerExtensions
    {
        public static ILoggingBuilder AddCentralLogger(this ILoggingBuilder builder, string serviceName, string loggingUrl)
        {
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSingleton<ILoggerProvider>(sp =>
            {
                var access = sp.GetRequiredService<IHttpContextAccessor>();
                return new CentralLoggerProvider(access, serviceName, loggingUrl);
            });
            return builder;
        }
    }
}
