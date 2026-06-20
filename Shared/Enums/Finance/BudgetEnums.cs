namespace Shared.Enums.Finance;

public enum BudgetType : byte
{
    Fixed      = 1,
    Percentage = 2,
    Annual     = 3,
    Flexible   = 4
}

public enum BudgetPeriodType : byte
{
    Monthly   = 1,
    Quarterly = 2,
    Yearly    = 3
}

public enum BudgetStatus : byte
{
    Active   = 1,
    Paused   = 2,
    Archived = 3
}

public enum BudgetPeriodStatus : byte
{
    Active   = 1,
    Exceeded = 2,
    Closed   = 3
}

public enum BudgetHealthBand : byte
{
    Poor      = 1,
    Fair      = 2,
    Good      = 3,
    Excellent = 4
}

public enum BudgetForecastRisk : byte
{
    Low    = 1,
    Medium = 2,
    High   = 3
}
