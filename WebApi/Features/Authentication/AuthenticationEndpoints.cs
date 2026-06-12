using Application.Features.Authentication.DTOs;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using WebApi.Common.Extensions;
using WebApi.Common.Filters;

namespace WebApi.Features.Authentication;

public static class AuthenticationEndpoints
{
    public static void MapAuthenticationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/authentication").WithTags("Authentication");

        group.MapPost("/register", RegisterAsync)
             .DisableAntiforgery()
             .AddEndpointFilter<ValidationFilter<RegisterRequest>>();
    }

    private static async Task<IResult> RegisterAsync(
        [FromForm] RegisterRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        var result = await authService.RegisterAsync(request, ct);
        return result.ToHttpResponse();
    }
}
