namespace Shared.Enums.CashFlow;

public enum ForecastRiskType : byte
{
    NegativeBalance      = 1,
    CashShortage         = 2,
    GoalAtRisk           = 3,
    SubscriptionRenewal  = 4,
    HighExpenseVariance  = 5,
}
