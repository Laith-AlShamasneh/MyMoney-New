using Application.Interfaces.Services;
using FluentValidation;
using Shared.Enums.System;
using Shared.Responses;

namespace WebApi.Common.Filters;

// Generic endpoint filter that validates any request type that has a registered IValidator<TRequest>.
// Validation error messages are message keys — translated via IMessageProvider before returning.
public sealed class ValidationFilter<TRequest>(IMessageProvider messageProvider) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var validator = context.HttpContext.RequestServices.GetService<IValidator<TRequest>>();

        if (validator is not null)
        {
            var request = context.Arguments.OfType<TRequest>().FirstOrDefault();

            if (request is not null)
            {
                var result = await validator.ValidateAsync(request);

                if (!result.IsValid)
                {
                    var messages = new List<string>();

                    foreach (var error in result.Errors)
                    {
                        var translated = await messageProvider.GetMessagesAsync(error.ErrorMessage);
                        messages.Add(translated);
                    }

                    var response = ApiResponse<object?>.Fail(
                        (int)InternalResponseCodes.BadRequest,
                        string.Join(" | ", messages));

                    return Results.Json(response, statusCode: StatusCodes.Status400BadRequest);
                }
            }
        }

        return await next(context);
    }
}
