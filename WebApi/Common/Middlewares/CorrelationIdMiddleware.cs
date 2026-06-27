using Serilog.Context;

namespace WebApi.Common.Middlewares;

// Assigns a correlation id to every request: honours an inbound X-Correlation-Id
// header (so a call chain shares one id) or generates a new GUID. The id is echoed
// on the response and pushed to Serilog's LogContext so every log line for the
// request carries it. Registered first so even early-pipeline logs are correlated.
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId =
            context.Request.Headers.TryGetValue(HeaderName, out var inbound)
            && !string.IsNullOrWhiteSpace(inbound)
                ? inbound.ToString()
                : Guid.NewGuid().ToString();

        // Set the response header before the body starts streaming.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
