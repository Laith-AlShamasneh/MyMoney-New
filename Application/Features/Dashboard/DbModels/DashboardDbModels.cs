namespace Application.Features.Dashboard.DbModels;

public sealed record DashboardKpiDbResult(
    decimal CurrentIncome,
    decimal CurrentExpenses,
    int     CurrentTransactionCount,
    decimal PreviousIncome,
    decimal PreviousExpenses,
    int     PreviousTransactionCount
);

public sealed record MonthlyTrendDbResult(
    int     Year,
    int     Month,
    decimal Income,
    decimal Expenses
);

public sealed record CategoryBreakdownDbResult(
    int     CategoryId,
    string  NameEn,
    string? NameAr,
    decimal TotalAmount
);

public sealed record RecentTransactionDbResult(
    long    TransactionId,
    decimal Amount,
    byte    TransactionTypeId,
    string? Description,
    string  TransactionDate,
    string  CategoryNameEn,
    string? CategoryNameAr,
    string? CategoryIcon
);
