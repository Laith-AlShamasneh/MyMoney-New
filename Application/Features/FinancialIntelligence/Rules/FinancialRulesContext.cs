namespace Application.Features.FinancialIntelligence.Rules;

/// <summary>
/// Immutable data snapshot passed to the rules engine. All DB reads happen
/// before evaluation so the engine itself has no infrastructure dependencies.
/// </summary>
public sealed class FinancialRulesContext
{
    public long    UserId { get; init; }
    public int     Year   { get; init; }
    public int     Month  { get; init; }

    public SnapshotData?                     CurrentSnapshot      { get; init; }
    public SnapshotData?                     PreviousSnapshot     { get; init; }
    public IReadOnlyList<SnapshotData>       RecentSnapshots      { get; init; } = [];

    public IReadOnlyList<CategoryPeriodData>      CurrentCategoryData     { get; init; } = [];
    public IReadOnlyList<CategoryPeriodData>      PreviousCategoryData    { get; init; } = [];

    // Pre-computed average of the 3 prior months per category (CategoryId → avg TotalSpent).
    // Used by EvaluateOverspendingAlert for a genuine rolling baseline.
    public IReadOnlyDictionary<int, decimal>      CategoryRollingAverages { get; init; } = new Dictionary<int, decimal>();
}

public sealed class SnapshotData
{
    public int     Year                   { get; init; }
    public int     Month                  { get; init; }
    public decimal TotalIncome            { get; init; }
    public decimal TotalExpense           { get; init; }
    public decimal NetBalance             { get; init; }
    public decimal AverageDailySpend      { get; init; }
    public decimal AverageTransactionValue { get; init; }
    public int     TransactionCount       { get; init; }
}

public sealed class CategoryPeriodData
{
    public int     CategoryId    { get; init; }
    public string  NameEn        { get; init; } = null!;
    public string  NameAr        { get; init; } = null!;
    public decimal TotalSpent    { get; init; }
    public int     TxCount       { get; init; }
    public decimal PercentOfTotal { get; init; }
}

/// <summary>
/// Candidate insight produced by a rule. The service persists it after dedup.
/// </summary>
public sealed class InsightCandidate
{
    public byte    Type              { get; init; }
    public string  Code              { get; init; } = null!;
    public string  TitleEn           { get; init; } = null!;
    public string  TitleAr           { get; init; } = null!;
    public string  DescriptionEn     { get; init; } = null!;
    public string  DescriptionAr     { get; init; } = null!;
    public byte    Severity          { get; init; }
    public int?    RelatedCategoryId { get; init; }
    public string? DataPointJson     { get; init; }
    /// <summary>True if this candidate should also fire an in-app notification.</summary>
    public bool    FireNotification  { get; init; }
    public string? NotificationCode  { get; init; }
    public Dictionary<string, string>? NotificationParameters { get; init; }
}

/// <summary>
/// Candidate recommendation produced by the engine.
/// </summary>
public sealed class RecommendationCandidate
{
    public byte     Type                { get; init; }
    public string   Code                { get; init; } = null!;
    public string   TitleEn             { get; init; } = null!;
    public string   TitleAr             { get; init; } = null!;
    public string   MessageEn           { get; init; } = null!;
    public string   MessageAr           { get; init; } = null!;
    public decimal? ExpectedImpactValue { get; init; }
    public byte     Priority            { get; init; }
    public int?     RelatedCategoryId   { get; init; }
}
