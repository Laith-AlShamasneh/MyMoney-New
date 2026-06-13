using Application.Features.Profile.DTOs;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using WebApi.Common.Extensions;
using WebApi.Common.Filters;

namespace WebApi.Features.Profile;

public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/profile")
                       .WithTags("Profile")
                       .RequireAuthorization();

        // ── Profile CRUD ─────────────────────────────────────────────────────
        group.MapPost("/get", GetProfileAsync);

        group.MapPost("/update", UpdateProfileAsync)
             .AddEndpointFilter<ValidationFilter<UpdateProfileRequest>>();

        group.MapPost("/picture/update", UpdateProfilePictureAsync)
             .DisableAntiforgery()
             .AddEndpointFilter<ValidationFilter<UpdateProfilePictureRequest>>();

        group.MapPost("/picture/remove", RemoveProfilePictureAsync);

        // ── Sessions ─────────────────────────────────────────────────────────
        group.MapPost("/sessions/list", GetSessionsAsync);

        group.MapPost("/sessions/revoke", RevokeSessionAsync)
             .AddEndpointFilter<ValidationFilter<RevokeSessionRequest>>();

        group.MapPost("/sessions/revoke-others", RevokeAllOtherSessionsAsync);

    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private static async Task<IResult> GetProfileAsync(
        IProfileService   profileService,
        CancellationToken ct)
    {
        var result = await profileService.GetProfileAsync(ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> UpdateProfileAsync(
        UpdateProfileRequest request,
        IProfileService      profileService,
        CancellationToken    ct)
    {
        var result = await profileService.UpdateProfileAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> UpdateProfilePictureAsync(
        [FromForm] UpdateProfilePictureRequest request,
        IProfileService                        profileService,
        CancellationToken                      ct)
    {
        var result = await profileService.UpdateProfilePictureAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> RemoveProfilePictureAsync(
        IProfileService   profileService,
        CancellationToken ct)
    {
        var result = await profileService.RemoveProfilePictureAsync(ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> GetSessionsAsync(
        HttpContext       httpContext,
        IProfileService   profileService,
        CancellationToken ct)
    {
        // Current refresh token passed via header to avoid request-body logging
        var currentToken = httpContext.Request.Headers["X-Refresh-Token"].FirstOrDefault();
        var result = await profileService.GetSessionsAsync(currentToken, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> RevokeSessionAsync(
        RevokeSessionRequest request,
        IProfileService      profileService,
        CancellationToken    ct)
    {
        var result = await profileService.RevokeSessionAsync(request.SessionId, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> RevokeAllOtherSessionsAsync(
        HttpContext       httpContext,
        IProfileService   profileService,
        CancellationToken ct)
    {
        var currentToken = httpContext.Request.Headers["X-Refresh-Token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(currentToken))
            return Results.BadRequest(new { success = false, message = "X-Refresh-Token header is required." });

        var result = await profileService.RevokeAllOtherSessionsAsync(currentToken, ct);
        return result.ToHttpResponse();
    }

}
