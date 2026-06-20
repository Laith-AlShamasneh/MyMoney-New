namespace Application.Features.Budget.DTOs;

// ── Requests ──────────────────────────────────────────────────────────────────

public sealed record CreateBudgetRequest(
    string   Name,
    int?     CategoryId,
    int      BudgetTypeId,
    decimal  Amount,
    int      PeriodTypeId,
    string   StartDate,
    string?  EndDate,
    bool     IsAutoRenew,
    string?  Notes);

public sealed record UpdateBudgetRequest(
    long     Id,
    string   Name,
    decimal  Amount,
    string?  EndDate,
    bool     IsAutoRenew,
    string?  Notes);

public sealed record GetBudgetRequest(long Id);

public sealed record GetBudgetListRequest(int? StatusId = null);

public sealed record DeleteBudgetRequest(long Id);

public sealed record PauseBudgetRequest(long Id);

public sealed record ResumeBudgetRequest(long Id);

public sealed record GetBudgetPeriodsRequest(
    long Id,
    int  PageNumber = 1,
    int  PageSize   = 12);

public sealed record GetBudgetAnalyticsRequest(
    long?   BudgetId = null,
    string? DateFrom = null,
    string? DateTo   = null);

// ── Period snapshot (shared sub-object) ──────────────────────────────────────

public sealed record BudgetPeriodSnapshot(
    long    PeriodId,
    string  PeriodStart,
    string  PeriodEnd,
    decimal BudgetedAmount,
    decimal ActualSpent,
    decimal UtilizationPct,
    decimal RemainingAmount,
    decimal OverBudgetAmount,
    decimal ProjectedEndSpending,
    decimal? DailyBudgetRemaining,
    int     ForecastRiskId,
    int     HealthScore,
    int     HealthBandId,
    int     PeriodStatusId);

// ── Responses ─────────────────────────────────────────────────────────────────

public sealed record BudgetResponse(
    long    BudgetId,
    string  Name,
    int?    CategoryId,
    string? CategoryNameEn,
    string? CategoryNameAr,
    string? CategoryIcon,
    int     BudgetTypeId,
    decimal Amount,
    int     PeriodTypeId,
    string  StartDate,
    string? EndDate,
    bool    IsAutoRenew,
    int     StatusId,
    string? Notes,
    string  CreatedAt,
    BudgetPeriodSnapshot? CurrentPeriod);

public sealed record BudgetDetailResponse(
    long    BudgetId,
    string  Name,
    int?    CategoryId,
    string? CategoryNameEn,
    string? CategoryNameAr,
    string? CategoryIcon,
    int     BudgetTypeId,
    decimal Amount,
    int     PeriodTypeId,
    string  StartDate,
    string? EndDate,
    bool    IsAutoRenew,
    int     StatusId,
    string? Notes,
    string  CreatedAt,
    string? UpdatedAt,
    string? ComputedAt,
    BudgetPeriodSnapshot? CurrentPeriod,
    IReadOnlyList<BudgetPeriodHistoryItem> History);

public sealed record BudgetPeriodHistoryItem(
    long    PeriodId,
    string  PeriodStart,
    string  PeriodEnd,
    decimal BudgetedAmount,
    decimal ActualSpent,
    decimal UtilizationPct,
    decimal OverBudgetAmount,
    int     HealthScore,
    int     HealthBandId,
    string? ClosedAt);

public sealed record BudgetDashboardSummaryResponse(
    int     TotalBudgets,
    int     ExceededCount,
    int     NearLimitCount,
    int     OnTrackCount,
    int     OverallHealthScore,
    decimal TotalRemainingAmount,
    decimal TotalBudgetedAmount,
    decimal TotalActualSpent);

public sealed record BudgetTrendPoint(
    string  PeriodStart,
    decimal AvgUtilizationPct,
    decimal TotalBudgeted,
    decimal TotalSpent,
    int     ExceededCount,
    int     AvgHealthScore);

public sealed record BudgetDashboardResponse(
    BudgetDashboardSummaryResponse  Summary,
    IReadOnlyList<BudgetResponse>   Budgets,
    IReadOnlyList<BudgetTrendPoint> Trend);

public sealed record BudgetPeriodListResponse(
    IReadOnlyList<BudgetPeriodHistoryItem> Items,
    int TotalCount);

public sealed record BudgetAnalyticsItem(
    long    BudgetId,
    string  BudgetName,
    int?    CategoryId,
    string? CategoryNameEn,
    string? CategoryNameAr,
    int     PeriodTypeId,
    long    PeriodId,
    string  PeriodStart,
    string  PeriodEnd,
    decimal BudgetedAmount,
    decimal ActualSpent,
    decimal UtilizationPct,
    decimal OverBudgetAmount,
    int     HealthScore,
    int     HealthBandId,
    int     ForecastRiskId,
    int     PeriodStatusId,
    string? ClosedAt);
