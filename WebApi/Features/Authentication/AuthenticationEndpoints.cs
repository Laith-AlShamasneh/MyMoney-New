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
        var group = app.MapGroup("/api/authentication").WithTags("Authentication")
                       .RequireRateLimiting("auth");

        group.MapPost("/register", RegisterAsync)
             .DisableAntiforgery()
             .AddEndpointFilter<ValidationFilter<RegisterRequest>>();

        group.MapPost("/login", LoginAsync)
             .AddEndpointFilter<ValidationFilter<LoginRequest>>();

        group.MapPost("/confirm-email", ConfirmEmailAsync)
             .AddEndpointFilter<ValidationFilter<ConfirmEmailRequest>>();

        group.MapPost("/resend-confirmation-email", ResendConfirmationEmailAsync)
             .AddEndpointFilter<ValidationFilter<ResendConfirmationEmailRequest>>();

        group.MapPost("/change-password", ChangePasswordAsync)
             .RequireAuthorization()
             .AddEndpointFilter<ValidationFilter<ChangePasswordRequest>>();

        group.MapPost("/forgot-password", ForgotPasswordAsync)
             .AddEndpointFilter<ValidationFilter<ForgotPasswordRequest>>();

        group.MapPost("/validate-reset-password-token", ValidateResetTokenAsync)
             .AddEndpointFilter<ValidationFilter<ValidateResetTokenRequest>>();

        group.MapPost("/reset-password", ResetPasswordAsync)
             .AddEndpointFilter<ValidationFilter<ResetPasswordRequest>>();

        group.MapPost("/refresh-token", RefreshTokenAsync);

        group.MapPost("/logout", LogoutAsync);

        // ── Email change ──────────────────────────────────────────────────────
        group.MapPost("/email-change/request", RequestEmailChangeAsync)
             .RequireAuthorization()
             .AddEndpointFilter<ValidationFilter<RequestEmailChangeRequest>>();

        // Confirm is public — user arrives from email link, JS page POSTs the token
        app.MapPost("/api/authentication/email-change/confirm", ConfirmEmailChangeAsync)
           .WithTags("Authentication")
           .RequireRateLimiting("auth")
           .AddEndpointFilter<ValidationFilter<ConfirmEmailChangeRequest>>();

        group.MapPost("/email-change/cancel", CancelEmailChangeAsync)
             .RequireAuthorization();
    }

    private static async Task<IResult> RegisterAsync(
        [FromForm] RegisterRequest request,
        IAuthService               authService,
        CancellationToken          ct)
    {
        var result = await authService.RegisterAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest      request,
        IAuthService      authService,
        CancellationToken ct)
    {
        var result = await authService.LoginAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> ConfirmEmailAsync(
        ConfirmEmailRequest request,
        IAuthService        authService,
        CancellationToken   ct)
    {
        var result = await authService.ConfirmEmailAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> ResendConfirmationEmailAsync(
        ResendConfirmationEmailRequest request,
        IAuthService                   authService,
        CancellationToken              ct)
    {
        var result = await authService.ResendConfirmationEmailAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        IAuthService          authService,
        CancellationToken     ct)
    {
        var result = await authService.ChangePasswordAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        IAuthService          authService,
        CancellationToken     ct)
    {
        var result = await authService.ForgotPasswordAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> ValidateResetTokenAsync(
        ValidateResetTokenRequest request,
        IAuthService              authService,
        CancellationToken         ct)
    {
        var result = await authService.ValidateResetTokenAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        IAuthService         authService,
        CancellationToken    ct)
    {
        var result = await authService.ResetPasswordAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> RefreshTokenAsync(
        RefreshTokenRequest request,
        HttpContext         httpContext,
        IAuthService        authService,
        CancellationToken   ct)
    {
        // Accept refresh token from X-Refresh-Token header (preferred) or request body (backward compat)
        var token = !string.IsNullOrWhiteSpace(request.RefreshToken)
            ? request.RefreshToken
            : httpContext.Request.Headers["X-Refresh-Token"].FirstOrDefault();

        var result = await authService.RefreshTokenAsync(request with { RefreshToken = token ?? string.Empty }, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> LogoutAsync(
        LogoutRequest     request,
        IAuthService      authService,
        CancellationToken ct)
    {
        var result = await authService.LogoutAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> RequestEmailChangeAsync(
        RequestEmailChangeRequest request,
        IAuthService              authService,
        CancellationToken         ct)
    {
        var result = await authService.RequestEmailChangeAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> ConfirmEmailChangeAsync(
        ConfirmEmailChangeRequest request,
        IAuthService              authService,
        CancellationToken         ct)
    {
        var result = await authService.ConfirmEmailChangeAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> CancelEmailChangeAsync(
        IAuthService      authService,
        CancellationToken ct)
    {
        var result = await authService.CancelEmailChangeAsync(ct);
        return result.ToHttpResponse();
    }
}
