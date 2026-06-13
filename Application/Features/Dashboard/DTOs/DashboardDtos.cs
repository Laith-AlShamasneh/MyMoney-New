namespace Application.Features.Dashboard.DTOs;

public sealed record DashboardSummaryResponse(
    KpiSummary                           Kpi,
    IReadOnlyList<MonthlyTrendItem>      MonthlyTrend,
    IReadOnlyList<CategoryBreakdownItem> CategoryBreakdown,
    IReadOnlyList<RecentTransactionItem> RecentTransactions
);

public sealed record KpiSummary(
    decimal  CurrentIncome,
    decimal  CurrentExpenses,
    decimal  CurrentNet,
    int      CurrentTransactionCount,
    decimal? IncomeChangePercent,
    decimal? ExpensesChangePercent,
    decimal? NetChangePercent,
    int?     TransactionCountChange
);

public sealed record MonthlyTrendItem(
    int     Year,
    int     Month,
    decimal Income,
    decimal Expenses
);

public sealed record CategoryBreakdownItem(
    int     CategoryId,
    string  NameEn,
    string? NameAr,
    decimal TotalAmount,
    decimal Percentage
);

public sealed record RecentTransactionItem(
    long    TransactionId,
    decimal Amount,
    int     TransactionTypeId,
    string? Description,
    string  TransactionDate,
    string  CategoryNameEn,
    string? CategoryNameAr,
    string? CategoryIcon
);
