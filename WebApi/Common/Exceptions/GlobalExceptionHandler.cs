using Application.Interfaces.Services;
using Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Shared.Enums.System;
using Shared.Constants;
using Shared.Responses;

namespace WebApi.Common.Exceptions;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IServiceScopeFactory scopeFactory) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken ct)
    {
        logger.LogError(exception, "Unhandled exception at {Path}", httpContext.Request.Path);

        using var scope           = scopeFactory.CreateScope();
        var       messageProvider = scope.ServiceProvider.GetRequiredService<IMessageProvider>();

        var (internalCode, messageKey) = exception switch
        {
            ValidationAppException  => (InternalResponseCodes.BadRequest,          MessageKeys.Common.BadRequest),
            UnauthorizedAccessException => (InternalResponseCodes.Unauthorized,    MessageKeys.Common.Unauthorized),
            ForbiddenException      => (InternalResponseCodes.Forbidden,           MessageKeys.Common.Forbidden),
            NotFoundException       => (InternalResponseCodes.NotFound,            MessageKeys.Common.NotFound),
            DomainException         => (InternalResponseCodes.BadRequest,          MessageKeys.Common.BadRequest),
            _                       => (InternalResponseCodes.InternalServerError, MessageKeys.Common.InternalServerError)
        };

        var httpStatusCode = exception switch
        {
            ValidationAppException      => StatusCodes.Status200OK,
            UnauthorizedAccessException => StatusCodes.Status200OK,
            ForbiddenException          => StatusCodes.Status200OK,
            NotFoundException           => StatusCodes.Status200OK,
            DomainException             => StatusCodes.Status200OK,
            _                           => StatusCodes.Status500InternalServerError
        };

        var message  = await messageProvider.GetMessagesAsync(messageKey, ct);
        var response = ApiResponse<object?>.Fail((int)internalCode, message);

        httpContext.Response.StatusCode = httpStatusCode;
        await httpContext.Response.WriteAsJsonAsync(response, ct);

        return true;
    }
}
