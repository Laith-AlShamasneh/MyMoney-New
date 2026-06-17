using Application.Features.FinancialIntelligence.DTOs;
using Shared.Results;

namespace Application.Features.FinancialIntelligence;

public interface IFinancialIntelligenceService
{
    Task<ServiceResult<InsightListResponse>>            GetInsightsAsync(GetInsightsRequest request, CancellationToken ct = default);
    Task<ServiceResult<object?>>                        MarkInsightReadAsync(MarkInsightReadRequest request, CancellationToken ct = default);
    Task<ServiceResult<object?>>                        MarkAllInsightsReadAsync(CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<PatternResponse>>> GetPatternsAsync(CancellationToken ct = default);
    Task<ServiceResult<RecommendationListResponse>>     GetRecommendationsAsync(GetRecommendationsRequest request, CancellationToken ct = default);
    Task<ServiceResult<object?>>                        MarkRecommendationAppliedAsync(MarkRecommendationAppliedRequest request, CancellationToken ct = default);
    Task<ServiceResult<object?>>                        DismissRecommendationAsync(DismissRecommendationRequest request, CancellationToken ct = default);
    Task<ServiceResult<FILDashboardResponse>>           GetDashboardAsync(CancellationToken ct = default);
}
