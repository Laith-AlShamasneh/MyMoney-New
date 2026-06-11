using Application.Interfaces.Services;
using Serilog.Context;
using System.Text;

namespace WebApi.Common.Middlewares;

public sealed class RequestLoggingMiddleware(RequestDelegate next)
{
    private const long MaxBodyLength = 32_768;

    public async Task InvokeAsync(HttpContext context, IUserContext userContext)
    {
        var request = context.Request;
        var path    = request.Path.Value ?? string.Empty;
        string? body = null;

        if (request.HasFormContentType && request.ContentType?.Contains("multipart/form-data") == true)
        {
            body = "[Multipart Content Skipped]";
        }
        else if (request.ContentLength > 0 && request.ContentLength < MaxBodyLength)
        {
            // Redact sensitive endpoints
            if (path.Contains("/start", StringComparison.OrdinalIgnoreCase)
             || path.Contains("/login", StringComparison.OrdinalIgnoreCase))
            {
                body = "[REDACTED SENSITIVE DATA]";
            }
            else
            {
                request.EnableBuffering();
                using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
                body = await reader.ReadToEndAsync();
                request.Body.Position = 0;
            }
        }

        using (LogContext.PushProperty("UserId",        userContext.UserId))
        using (LogContext.PushProperty("IPAddress",     userContext.IpAddress))
        using (LogContext.PushProperty("RequestPath",   path))
        using (LogContext.PushProperty("RequestMethod", request.Method))
        using (LogContext.PushProperty("RequestBody",   body))
        {
            await next(context);
        }
    }
}
