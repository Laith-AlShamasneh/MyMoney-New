using Application.Features.FinancialIntelligence;
using Application.Features.FinancialIntelligence.DTOs;
using WebApi.Common.Extensions;
using WebApi.Common.Filters;

namespace WebApi.Features.FinancialIntelligence;

public static class FinancialIntelligenceEndpoints
{
    public static void MapFinancialIntelligenceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("api/financial-intelligence")
                       .WithTags("FinancialIntelligence")
                       .RequireAuthorization();

        group.MapPost("dashboard", async (
            IFinancialIntelligenceService service,
            CancellationToken             ct) =>
        {
            var result = await service.GetDashboardAsync(ct);
            return result.ToHttpResponse();
        });

        group.MapPost("insights", async (
            GetInsightsRequest            request,
            IFinancialIntelligenceService service,
            CancellationToken             ct) =>
        {
            var result = await service.GetInsightsAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetInsightsRequest>>();

        group.MapPost("insights/mark-read", async (
            MarkInsightReadRequest        request,
            IFinancialIntelligenceService service,
            CancellationToken             ct) =>
        {
            var result = await service.MarkInsightReadAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<MarkInsightReadRequest>>();

        group.MapPost("insights/mark-all-read", async (
            IFinancialIntelligenceService service,
            CancellationToken             ct) =>
        {
            var result = await service.MarkAllInsightsReadAsync(ct);
            return result.ToHttpResponse();
        });

        group.MapPost("patterns", async (
            IFinancialIntelligenceService service,
            CancellationToken             ct) =>
        {
            var result = await service.GetPatternsAsync(ct);
            return result.ToHttpResponse();
        });

        group.MapPost("recommendations", async (
            GetRecommendationsRequest     request,
            IFinancialIntelligenceService service,
            CancellationToken             ct) =>
        {
            var result = await service.GetRecommendationsAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetRecommendationsRequest>>();

        group.MapPost("recommendations/apply", async (
            MarkRecommendationAppliedRequest request,
            IFinancialIntelligenceService    service,
            CancellationToken                ct) =>
        {
            var result = await service.MarkRecommendationAppliedAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<MarkRecommendationAppliedRequest>>();

        group.MapPost("recommendations/dismiss", async (
            DismissRecommendationRequest  request,
            IFinancialIntelligenceService service,
            CancellationToken             ct) =>
        {
            var result = await service.DismissRecommendationAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<DismissRecommendationRequest>>();
    }
}
