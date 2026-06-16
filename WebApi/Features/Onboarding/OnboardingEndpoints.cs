using Application.Features.Onboarding.DTOs;
using Application.Interfaces.Services;
using WebApi.Common.Extensions;
using WebApi.Common.Filters;

namespace WebApi.Features.Onboarding;

public static class OnboardingEndpoints
{
    public static void MapOnboardingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/onboarding")
                       .WithTags("Onboarding")
                       .RequireAuthorization();

        group.MapPost("/state",   GetStateAsync);
        group.MapPost("/advance", AdvanceStepAsync)
             .AddEndpointFilter<ValidationFilter<AdvanceStepRequest>>();
        group.MapPost("/skip",    SkipAsync);
    }

    private static async Task<IResult> GetStateAsync(
        IOnboardingService onboardingService,
        CancellationToken  ct)
    {
        var result = await onboardingService.GetStateAsync(ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> AdvanceStepAsync(
        AdvanceStepRequest request,
        IOnboardingService onboardingService,
        CancellationToken  ct)
    {
        var result = await onboardingService.AdvanceStepAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> SkipAsync(
        IOnboardingService onboardingService,
        CancellationToken  ct)
    {
        var result = await onboardingService.SkipAsync(ct);
        return result.ToHttpResponse();
    }
}
