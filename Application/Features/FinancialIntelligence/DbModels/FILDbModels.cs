namespace Application.Features.FinancialIntelligence.DbModels;

// ── Snapshot ──────────────────────────────────────────────────────────────────

public class UpsertSnapshotDbModel
{
    public long     UserId                  { get; set; }
    public long?    WorkspaceId             { get; set; }
    public DateOnly SnapshotDate            { get; set; }
    public byte     PeriodType              { get; set; }  // SnapshotPeriodType
    public decimal  TotalIncome             { get; set; }
    public decimal  TotalExpense            { get; set; }
    public decimal  NetBalance              { get; set; }
    public decimal  AverageDailySpend       { get; set; }
    public decimal  AverageTransactionValue { get; set; }
    public int      TransactionCount        { get; set; }
    public int?     TopCategoryId           { get; set; }
}

public class SnapshotDbResult
{
    public long     SnapshotId              { get; set; }
    public DateOnly SnapshotDate            { get; set; }
    public byte     PeriodType              { get; set; }
    public decimal  TotalIncome             { get; set; }
    public decimal  TotalExpense            { get; set; }
    public decimal  NetBalance              { get; set; }
    public decimal  AverageDailySpend       { get; set; }
    public decimal  AverageTransactionValue { get; set; }
    public int      TransactionCount        { get; set; }
    public int?     TopCategoryId           { get; set; }
    public DateTime UpdatedAtUtc            { get; set; }
}

public class ComputeSnapshotDbModel
{
    public long  UserId      { get; set; }
    public long? WorkspaceId { get; set; }
    public int   Year        { get; set; }
    public int   Month       { get; set; }
}

public class ComputedSnapshotDbResult
{
    public decimal TotalIncome             { get; set; }
    public decimal TotalExpense            { get; set; }
    public decimal NetBalance              { get; set; }
    public decimal AverageDailySpend       { get; set; }
    public decimal AverageTransactionValue { get; set; }
    public int     TransactionCount        { get; set; }
    public int?    TopCategoryId           { get; set; }
}

// ── Category Analytics ────────────────────────────────────────────────────────

public class CategoryAnalyticsDbResult
{
    public long    Id                   { get; set; }
    public int     CategoryId           { get; set; }
    public string  CategoryNameEn       { get; set; } = null!;
    public string  CategoryNameAr       { get; set; } = null!;
    public decimal TotalSpent           { get; set; }
    public int     TransactionCount     { get; set; }
    public decimal AverageSpent         { get; set; }
    public decimal PercentageOfTotal    { get; set; }
    public byte    TrendDirection       { get; set; }   // TrendDirection enum
    public decimal PreviousPeriodTotal  { get; set; }
    public decimal ChangePercentage     { get; set; }
    public DateOnly PeriodStart         { get; set; }
    public DateOnly PeriodEnd           { get; set; }
}

public class ComputeCategoryAnalyticsDbModel
{
    public long  UserId      { get; set; }
    public long? WorkspaceId { get; set; }
    public int   Year        { get; set; }
    public int   Month       { get; set; }
}

// ── Insights ──────────────────────────────────────────────────────────────────

public class CreateInsightDbModel
{
    public long    UserId           { get; set; }
    public long?   WorkspaceId      { get; set; }
    public byte    Type             { get; set; }    // InsightType enum
    public string  Code             { get; set; } = null!;
    public string  TitleEn          { get; set; } = null!;
    public string  TitleAr          { get; set; } = null!;
    public string  DescriptionEn    { get; set; } = null!;
    public string  DescriptionAr    { get; set; } = null!;
    public byte    Severity         { get; set; }    // InsightSeverity enum
    public int?    RelatedCategoryId { get; set; }
    public string? DataPointJson    { get; set; }
    public DateTime? ExpiresAtUtc   { get; set; }
}

public class InsightRowDbResult
{
    public long      InsightId          { get; set; }
    public byte      Type               { get; set; }
    public string    Code               { get; set; } = null!;
    public string    TitleEn            { get; set; } = null!;
    public string    TitleAr            { get; set; } = null!;
    public string    DescriptionEn      { get; set; } = null!;
    public string    DescriptionAr      { get; set; } = null!;
    public byte      Severity           { get; set; }
    public int?      RelatedCategoryId  { get; set; }
    public string?   DataPointJson      { get; set; }
    public bool      IsRead             { get; set; }
    public DateTime  GeneratedAtUtc     { get; set; }
    public DateTime? ExpiresAtUtc       { get; set; }
}

public class GetInsightsDbModel
{
    public long  UserId      { get; set; }
    public long? WorkspaceId { get; set; }
    public bool? IsRead     { get; set; }
    public int   PageNumber { get; set; } = 1;
    public int   PageSize   { get; set; } = 20;
}

public class GetInsightsDbResult
{
    public IReadOnlyList<InsightRowDbResult> Items       { get; set; } = [];
    public int                              TotalCount  { get; set; }
    public int                              UnreadCount { get; set; }
}

// ── Spending Patterns ─────────────────────────────────────────────────────────

public class CreatePatternDbModel
{
    public long    UserId          { get; set; }
    public long?   WorkspaceId     { get; set; }
    public byte    PatternType     { get; set; }    // PatternType enum
    public string  Code            { get; set; } = null!;
    public string  DescriptionEn   { get; set; } = null!;
    public string  DescriptionAr   { get; set; } = null!;
    public decimal ConfidenceScore { get; set; }
    public string? DataPointJson   { get; set; }
    public DateTime? ValidUntilUtc { get; set; }
}

public class PatternDbResult
{
    public long     PatternId       { get; set; }
    public byte     PatternType     { get; set; }
    public string   Code            { get; set; } = null!;
    public string   DescriptionEn   { get; set; } = null!;
    public string   DescriptionAr   { get; set; } = null!;
    public decimal  ConfidenceScore { get; set; }
    public DateTime DetectedAtUtc   { get; set; }
}

// ── Recommendations ───────────────────────────────────────────────────────────

public class CreateRecommendationDbModel
{
    public long     UserId               { get; set; }
    public long?    WorkspaceId          { get; set; }
    public byte     Type                 { get; set; }    // RecommendationType enum
    public string   Code                 { get; set; } = null!;
    public string   TitleEn              { get; set; } = null!;
    public string   TitleAr              { get; set; } = null!;
    public string   MessageEn            { get; set; } = null!;
    public string   MessageAr            { get; set; } = null!;
    public decimal? ExpectedImpactValue  { get; set; }
    public byte     Priority             { get; set; }
    public int?     RelatedCategoryId    { get; set; }
    public DateTime? ExpiresAtUtc        { get; set; }
}

public class RecommendationDbResult
{
    public long      RecommendationId    { get; set; }
    public byte      Type                { get; set; }
    public string    Code                { get; set; } = null!;
    public string    TitleEn             { get; set; } = null!;
    public string    TitleAr             { get; set; } = null!;
    public string    MessageEn           { get; set; } = null!;
    public string    MessageAr           { get; set; } = null!;
    public decimal?  ExpectedImpactValue { get; set; }
    public byte      Priority            { get; set; }
    public int?      RelatedCategoryId   { get; set; }
    public bool      IsApplied           { get; set; }
    public bool      IsDismissed         { get; set; }
    public DateTime  CreatedAtUtc        { get; set; }
}

public class GetRecommendationsDbModel
{
    public long  UserId      { get; set; }
    public long? WorkspaceId { get; set; }
    public int  PageNumber { get; set; } = 1;
    public int  PageSize   { get; set; } = 10;
}

public class GetRecommendationsDbResult
{
    public IReadOnlyList<RecommendationDbResult> Items      { get; set; } = [];
    public int                                  TotalCount { get; set; }
}

// ── Large Transaction Detection ───────────────────────────────────────────────

public class LargeTransactionDbResult
{
    public long    WorkspaceId   { get; set; }
    public long    UserId        { get; set; }
    public long    TransactionId { get; set; }
    public decimal Amount        { get; set; }
    public decimal WsAverage     { get; set; }
    public string  CategoryNameEn { get; set; } = null!;
    public string  CategoryNameAr { get; set; } = null!;
}

// ── Workspaces for processing ─────────────────────────────────────────────────

public class ActiveUserDbResult
{
    public long WorkspaceId { get; set; }
    public long OwnerUserId { get; set; }
}
