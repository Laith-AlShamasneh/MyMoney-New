using Application.Features.FinancialIntelligence.DbModels;

namespace Application.Interfaces.Repositories;

public interface IFinancialIntelligenceRepository
{
    // ── Snapshots ─────────────────────────────────────────────────────────────
    Task<ComputedSnapshotDbResult?>               ComputeMonthlySnapshotAsync(ComputeSnapshotDbModel model, CancellationToken ct = default);
    Task                                          UpsertSnapshotAsync(UpsertSnapshotDbModel model, CancellationToken ct = default);
    Task<SnapshotDbResult?>                       GetLatestSnapshotAsync(long userId, CancellationToken ct = default);
    Task<IReadOnlyList<SnapshotDbResult>>         GetRecentSnapshotsAsync(long userId, int months, CancellationToken ct = default);

    // ── Category Analytics ────────────────────────────────────────────────────
    Task<IReadOnlyList<CategoryAnalyticsDbResult>> ComputeCategoryAnalyticsAsync(ComputeCategoryAnalyticsDbModel model, CancellationToken ct = default);
    Task                                           UpsertCategoryAnalyticsAsync(long userId, IReadOnlyList<CategoryAnalyticsDbResult> rows, CancellationToken ct = default);
    Task<IReadOnlyList<CategoryAnalyticsDbResult>> GetCategoryAnalyticsAsync(long userId, int year, int month, CancellationToken ct = default);

    // ── Insights ──────────────────────────────────────────────────────────────
    Task<long>                                    CreateInsightAsync(CreateInsightDbModel model, CancellationToken ct = default);
    Task<GetInsightsDbResult>                     GetInsightsAsync(GetInsightsDbModel model, CancellationToken ct = default);
    Task<int>                                     MarkInsightReadAsync(long userId, long insightId, CancellationToken ct = default);
    Task<bool>                                    InsightExistsForMonthAsync(long userId, string code, int year, int month, CancellationToken ct = default);
    Task                                          CleanupExpiredInsightsAsync(CancellationToken ct = default);

    // ── Spending Patterns ─────────────────────────────────────────────────────
    Task                                          UpsertPatternAsync(CreatePatternDbModel model, CancellationToken ct = default);
    Task<IReadOnlyList<PatternDbResult>>          GetPatternsAsync(long userId, CancellationToken ct = default);

    // ── Recommendations ───────────────────────────────────────────────────────
    Task<long>                                    CreateRecommendationAsync(CreateRecommendationDbModel model, CancellationToken ct = default);
    Task<GetRecommendationsDbResult>              GetRecommendationsAsync(GetRecommendationsDbModel model, CancellationToken ct = default);
    Task<int>                                     MarkRecommendationAppliedAsync(long userId, long recommendationId, CancellationToken ct = default);
    Task<int>                                     DismissRecommendationAsync(long userId, long recommendationId, CancellationToken ct = default);
    Task<bool>                                    RecommendationExistsForMonthAsync(long userId, string code, int year, int month, CancellationToken ct = default);

    // ── Large-transaction detection ───────────────────────────────────────────
    Task<IReadOnlyList<LargeTransactionDbResult>> GetRecentLargeTransactionsAsync(DateTime fromUtc, CancellationToken ct = default);

    // ── Active users ──────────────────────────────────────────────────────────
    Task<IReadOnlyList<ActiveUserDbResult>>       GetActiveUsersAsync(int activeDays, CancellationToken ct = default);
}
