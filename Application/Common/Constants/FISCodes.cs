namespace Application.Common.Constants;

/// <summary>
/// Insight code constants — mirror the Code column in FinancialInsights.
/// Public so test projects can reference them directly without duplicating strings.
/// </summary>
public static class InsightCodes
{
    public const string HighExpenseRatio   = "HighExpenseRatio";
    public const string SpendingSpike      = "SpendingSpike";
    public const string OverspendingAlert  = "OverspendingAlert";
    public const string PositiveBehavior   = "PositiveBehavior";
    public const string ConsistentSaver    = "ConsistentSaver";
    public const string UnusualTransaction = "UnusualTransaction";

    // Budget-related
    public const string BudgetAtRisk    = "BudgetAtRisk";
    public const string BudgetOverspend = "BudgetOverspend";
}

/// <summary>
/// Recommendation code constants — mirror the Code column in FinancialRecommendations.
/// Public so test projects can reference them directly without duplicating strings.
/// </summary>
public static class RecommendationCodes
{
    public const string ReviewTopCategory    = "ReviewTopCategory";
    public const string SavingsTarget20Pct   = "SavingsTarget20Pct";

    // Budget-related
    public const string ReduceCategorySpend = "ReduceCategorySpend";
    public const string AdjustBudget        = "AdjustBudget";
}
