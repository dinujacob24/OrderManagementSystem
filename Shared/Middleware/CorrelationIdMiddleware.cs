using Microsoft.AspNetCore.Http;

namespace Shared.Middleware;
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.ContainsKey("X-Correlation-Id"))
        {
            context.Request.Headers["X-Correlation-Id"] = Guid.NewGuid().ToString();
        }

        context.Response.Headers["X-Correlation-Id"] = context.Request.Headers["X-Correlation-Id"];
        await _next(context);
    }
}
