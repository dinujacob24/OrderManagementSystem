using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;

namespace Shared.Logging
{
    public static class LoggingExtensions
    {
        public static WebApplicationBuilder AddCentralLogging(this WebApplicationBuilder builder, string serviceName)
        {
            var loggingUrl = builder.Configuration["LoggingServiceUrl"] ?? "http://localhost:5017";

            // 1. Add HttpContextAccessor for CorrelationId and UserName tracking
            builder.Services.AddHttpContextAccessor();

            // 2. Register the manual Log Sender (The simplified "Brute Force")
            // We register the HttpClient with a specific name
            builder.Services.AddHttpClient("CentralInternal", client =>
            {
                // Ensure the URL ends with a / so that relative paths (like "logs") work correctly
                var baseUri = loggingUrl.EndsWith("/") ? loggingUrl : loggingUrl + "/";
                client.BaseAddress = new Uri(baseUri);
            });

            // Register ILogSender with a factory that uses that client and the service name
            builder.Services.AddScoped<ILogSender>(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var client = factory.CreateClient("CentralInternal");
                var accessor = sp.GetRequiredService<IHttpContextAccessor>();
                return new CentralLogSender(client, accessor, serviceName);
            });

            // 3. Plug CentralLogger into ASP.NET's ILogger pipeline so any ILogger<T>
            //    call in the app forwards to LoggingService.
            builder.Logging.AddCentralLogger(serviceName, loggingUrl);

            return builder;
        }
    }
}
