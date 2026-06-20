namespace Application.Features.CashFlow.DTOs;

// ── Request ───────────────────────────────────────────────────────────────────

public record GetForecastRequest(int HorizonMonths = 12);

// ── Monthly point ─────────────────────────────────────────────────────────────

public record MonthlyPointDto(
    string  MonthYear,
    decimal ProjectedIncome,
    decimal ProjectedExpense,
    decimal ProjectedNet,
    decimal RunningBalance,
    decimal RecurringIncome,
    decimal RecurringExpense,
    decimal VariableIncome,
    decimal VariableExpense,
    decimal ConfidenceScore);

// ── Risk ──────────────────────────────────────────────────────────────────────

public record ForecastRiskDto(
    long    RiskId,
    byte    RiskType,
    byte    Severity,
    string  Title,
    string  Description,
    string? AffectedMonth);

// ── Goal projection ──────────────────────────────────────────────────────────

public record GoalProjectionDto(
    long     GoalId,
    string   GoalName,
    decimal  TargetAmount,
    decimal  CurrentAmount,
    string?  TargetDate,
    decimal  RequiredMonthlyContribution,
    decimal  AvgMonthlyPace,
    string?  EstimatedCompletionDate,
    bool     IsAtRisk,
    int?     DaysToCompletion);

// ── Full forecast response ────────────────────────────────────────────────────

public record CashFlowForecastResponse(
    long                          ForecastId,
    string                        GeneratedAt,
    int                           HorizonMonths,
    int                           MonthsOfHistoryUsed,
    decimal                       CurrentBalanceEst,
    decimal                       OverallConfidence,
    byte                          ConfidenceBand,
    string                        ConfidenceBandLabel,
    decimal                       RecurringIncomeMonthly,
    decimal                       RecurringExpenseMonthly,
    decimal                       AvgVariableIncomeMonthly,
    decimal                       AvgVariableExpenseMonthly,
    decimal                       ForecastedEndBalance,
    IReadOnlyList<MonthlyPointDto>     MonthlyTimeline,
    IReadOnlyList<ForecastRiskDto>     Risks,
    IReadOnlyList<GoalProjectionDto>   GoalProjections);

// ── Dashboard response ────────────────────────────────────────────────────────

public record DashboardMonthlyPointDto(
    string  MonthYear,
    decimal ProjectedIncome,
    decimal ProjectedExpense,
    decimal ProjectedNet,
    decimal RunningBalance,
    decimal ConfidenceScore);

public record CashFlowDashboardResponse(
    long                                     ForecastId,
    string                                   GeneratedAt,
    decimal                                  OverallConfidence,
    byte                                     ConfidenceBand,
    string                                   ConfidenceBandLabel,
    decimal                                  CurrentBalanceEst,
    decimal                                  ForecastedEndBalance,
    int                                      MonthsOfHistoryUsed,
    decimal                                  RecurringIncomeMonthly,
    decimal                                  RecurringExpenseMonthly,
    IReadOnlyList<DashboardMonthlyPointDto>  NextMonths,
    IReadOnlyList<ForecastRiskDto>           TopRisks);
