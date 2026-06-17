using System.Text.Json;
using System.Text.Json.Serialization;

namespace Application.Features.FinancialIntelligence.DTOs;

// ── Requests ──────────────────────────────────────────────────────────────────

public record GetInsightsRequest(
    bool? IsRead,
    int   PageNumber = 1,
    int   PageSize   = 20);

public record MarkInsightReadRequest(long InsightId);

public record GetRecommendationsRequest(
    int PageNumber = 1,
    int PageSize   = 10);

public record MarkRecommendationAppliedRequest(long RecommendationId);
public record DismissRecommendationRequest(long RecommendationId);

// ── Responses ─────────────────────────────────────────────────────────────────

public sealed record SnapshotResponse(
    DateOnly SnapshotDate,
    decimal  TotalIncome,
    decimal  TotalExpense,
    decimal  NetBalance,
    decimal  AverageDailySpend,
    decimal  AverageTransactionValue,
    int      TransactionCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int?     TopCategoryId
);

public sealed record CategoryAnalyticsResponse(
    int      CategoryId,
    string   CategoryName,
    decimal  TotalSpent,
    int      TransactionCount,
    decimal  AverageSpent,
    decimal  PercentageOfTotal,
    byte     TrendDirection,
    string   TrendDirectionName,
    decimal  PreviousPeriodTotal,
    decimal  ChangePercentage
);

public sealed record InsightResponse(
    long     InsightId,
    byte     Type,
    string   TypeName,
    string   Code,
    string   Title,
    string   Description,
    byte     Severity,
    string   SeverityName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int?     RelatedCategoryId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? DataPointJson,
    bool     IsRead,
    DateTime GeneratedAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTime? ExpiresAtUtc
);

public sealed record InsightListResponse(
    IReadOnlyList<InsightResponse> Items,
    int                           TotalCount,
    int                           UnreadCount,
    int                           PageNumber,
    int                           PageSize
);

public sealed record PatternResponse(
    long     PatternId,
    byte     PatternType,
    string   PatternTypeName,
    string   Code,
    string   Description,
    decimal  ConfidenceScore,
    DateTime DetectedAtUtc
);

public sealed record RecommendationResponse(
    long     RecommendationId,
    byte     Type,
    string   TypeName,
    string   Code,
    string   Title,
    string   Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    decimal? ExpectedImpactValue,
    byte     Priority,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int?     RelatedCategoryId,
    bool     IsApplied,
    bool     IsDismissed,
    DateTime CreatedAtUtc
);

public sealed record RecommendationListResponse(
    IReadOnlyList<RecommendationResponse> Items,
    int                                  TotalCount,
    int                                  PageNumber,
    int                                  PageSize
);

public sealed record FILDashboardResponse(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    SnapshotResponse?                        LatestSnapshot,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    FinancialHealthScore?                    HealthScore,
    IReadOnlyList<InsightResponse>           TopInsights,
    IReadOnlyList<PatternResponse>           Patterns,
    IReadOnlyList<RecommendationResponse>    Recommendations,
    IReadOnlyList<CategoryAnalyticsResponse> CategoryTrends
);

// Rating values: "Healthy" (≥80), "Good" (≥60), "AtRisk" (≥35), "Poor" (<35).
// Frontend CSS class: "fil-score-" + rating.ToLower() where "AtRisk" → "fil-score-atrisk".
public sealed record FinancialHealthScore(
    int                              Score,
    string                           Rating,
    IReadOnlyList<HealthScoreFactor> Factors
);

// Value semantics per FactorKey:
//   SavingsRate  — savings rate as a percentage (e.g. 15.3 = 15.3%)
//   ExpenseRatio — expense-to-income ratio as a percentage
//   SpendingTrend — count of months where expenses rose >5% over the prior month (0–2)
//   BalanceStreak — count of recent months with positive NetBalance (0–3)
public sealed record HealthScoreFactor(
    string  FactorKey,
    int     Score,
    int     MaxScore,
    decimal Value
);
