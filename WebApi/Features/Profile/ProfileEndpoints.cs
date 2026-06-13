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
        group.MapGet("/", GetProfileAsync);

        group.MapPut("/", UpdateProfileAsync)
             .AddEndpointFilter<ValidationFilter<UpdateProfileRequest>>();

        group.MapPatch("/picture", UpdateProfilePictureAsync)
             .DisableAntiforgery()
             .AddEndpointFilter<ValidationFilter<UpdateProfilePictureRequest>>();

        group.MapDelete("/picture", RemoveProfilePictureAsync);

        // ── Sessions ─────────────────────────────────────────────────────────
        group.MapGet("/sessions", GetSessionsAsync);

        group.MapDelete("/sessions/{id:long}", RevokeSessionAsync);

        group.MapDelete("/sessions/others", RevokeAllOtherSessionsAsync);

        // ── Email change ─────────────────────────────────────────────────────
        group.MapPost("/email-change/request", RequestEmailChangeAsync)
             .AddEndpointFilter<ValidationFilter<RequestEmailChangeRequest>>();

        // Email change confirm is public — user clicks link in email (no JWT)
        app.MapGet("/api/profile/email-change/confirm", ConfirmEmailChangeAsync)
           .WithTags("Profile");

        group.MapDelete("/email-change", CancelEmailChangeAsync);
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
        // Current refresh token passed via header to avoid query-string logging
        var currentToken = httpContext.Request.Headers["X-Refresh-Token"].FirstOrDefault();
        var result = await profileService.GetSessionsAsync(currentToken, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> RevokeSessionAsync(
        long              id,
        IProfileService   profileService,
        CancellationToken ct)
    {
        var result = await profileService.RevokeSessionAsync(id, ct);
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

    private static async Task<IResult> RequestEmailChangeAsync(
        RequestEmailChangeRequest request,
        IProfileService           profileService,
        CancellationToken         ct)
    {
        var result = await profileService.RequestEmailChangeAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> ConfirmEmailChangeAsync(
        [FromQuery] string token,
        IProfileService    profileService,
        CancellationToken  ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Results.BadRequest(new { success = false, message = "Token is required." });

        var result = await profileService.ConfirmEmailChangeAsync(new ConfirmEmailChangeRequest(token), ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> CancelEmailChangeAsync(
        IProfileService   profileService,
        CancellationToken ct)
    {
        var result = await profileService.CancelEmailChangeAsync(ct);
        return result.ToHttpResponse();
    }
}
