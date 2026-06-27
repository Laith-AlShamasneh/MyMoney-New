namespace Application.Features.Budget.DbModels;

// ── Create ────────────────────────────────────────────────────────────────────

public class CreateBudgetDbModel
{
    public long     UserId        { get; set; }
    public long?    WorkspaceId   { get; set; }
    public string   Name          { get; set; } = null!;
    public int?     CategoryId    { get; set; }
    public byte     BudgetTypeId  { get; set; }
    public decimal  Amount        { get; set; }
    public byte     PeriodTypeId  { get; set; }
    public DateOnly StartDate     { get; set; }
    public DateOnly? EndDate      { get; set; }
    public bool     IsAutoRenew   { get; set; }
    public string?  Notes         { get; set; }
}

public class CreateBudgetDbResult
{
    public long    NewBudgetId { get; set; }
    public byte    ResultCode  { get; set; }  // 0=Success 1=Duplicate 2=InvalidCategory
}

// ── Update ────────────────────────────────────────────────────────────────────

public class UpdateBudgetDbModel
{
    public long     UserId       { get; set; }
    public long?    WorkspaceId  { get; set; }
    public long     BudgetId     { get; set; }
    public string   Name         { get; set; } = null!;
    public decimal  Amount       { get; set; }
    public DateOnly? EndDate     { get; set; }
    public bool     IsAutoRenew  { get; set; }
    public string?  Notes        { get; set; }
}

// ── Status ────────────────────────────────────────────────────────────────────

public class UpdateBudgetStatusDbModel
{
    public long  UserId      { get; set; }
    public long? WorkspaceId { get; set; }
    public long  BudgetId    { get; set; }
    public byte  NewStatusId { get; set; }
}

// ── Delete ────────────────────────────────────────────────────────────────────

public class DeleteBudgetDbModel
{
    public long  UserId      { get; set; }
    public long? WorkspaceId { get; set; }
    public long  BudgetId    { get; set; }
}

// ── Get List ──────────────────────────────────────────────────────────────────

public class GetBudgetListDbModel
{
    public long  UserId      { get; set; }
    public long? WorkspaceId { get; set; }
    public byte? StatusId    { get; set; }
}

// ── Get Periods ───────────────────────────────────────────────────────────────

public class GetBudgetPeriodsDbModel
{
    public long  UserId      { get; set; }
    public long? WorkspaceId { get; set; }
    public long  BudgetId    { get; set; }
    public int   PageNumber  { get; set; } = 1;
    public int   PageSize    { get; set; } = 12;
}

// ── Analytics ─────────────────────────────────────────────────────────────────

public class GetBudgetAnalyticsDbModel
{
    public long      UserId      { get; set; }
    public long?     WorkspaceId { get; set; }
    public long?     BudgetId    { get; set; }
    public DateOnly? DateFrom    { get; set; }
    public DateOnly? DateTo      { get; set; }
}

// ── DB Result rows ────────────────────────────────────────────────────────────

public class BudgetRowDbResult
{
    public long     BudgetId             { get; set; }
    public string   Name                 { get; set; } = null!;
    public int?     CategoryId           { get; set; }
    public string?  CategoryNameEn       { get; set; }
    public string?  CategoryNameAr       { get; set; }
    public string?  CategoryIcon         { get; set; }
    public byte     BudgetTypeId         { get; set; }
    public decimal  Amount               { get; set; }
    public byte     PeriodTypeId         { get; set; }
    public DateOnly StartDate            { get; set; }
    public DateOnly? EndDate             { get; set; }
    public bool     IsAutoRenew          { get; set; }
    public byte     StatusId             { get; set; }
    public string?  Notes                { get; set; }
    public DateTime CreatedAtUtc         { get; set; }

    // Current period snapshot (may be null if no active period exists)
    public long?    PeriodId             { get; set; }
    public DateOnly? PeriodStart         { get; set; }
    public DateOnly? PeriodEnd           { get; set; }
    public decimal  BudgetedAmount       { get; set; }
    public decimal  ActualSpent          { get; set; }
    public decimal  UtilizationPct       { get; set; }
    public decimal  RemainingAmount      { get; set; }
    public decimal  OverBudgetAmount     { get; set; }
    public decimal  ProjectedEndSpending { get; set; }
    public decimal? DailyBudgetRemaining { get; set; }
    public byte     ForecastRiskId       { get; set; }
    public byte     HealthScore          { get; set; }
    public byte     HealthBandId         { get; set; }
    public byte     PeriodStatusId       { get; set; }
}

public class BudgetDetailDbResult : BudgetRowDbResult
{
    public DateTime? UpdatedAtUtc  { get; set; }
    public DateTime? ComputedAtUtc { get; set; }
}

public class BudgetPeriodRowDbResult
{
    public long     PeriodId             { get; set; }
    public DateOnly PeriodStart          { get; set; }
    public DateOnly PeriodEnd            { get; set; }
    public decimal  BudgetedAmount       { get; set; }
    public decimal  ActualSpent          { get; set; }
    public decimal  UtilizationPct       { get; set; }
    public decimal  RemainingAmount      { get; set; }
    public decimal  OverBudgetAmount     { get; set; }
    public decimal  ProjectedEndSpending { get; set; }
    public byte     ForecastRiskId       { get; set; }
    public byte     HealthScore          { get; set; }
    public byte     HealthBandId         { get; set; }
    public byte     StatusId             { get; set; }
    public DateTime? ClosedAtUtc         { get; set; }
    public DateTime CreatedAtUtc         { get; set; }
    public int      TotalCount           { get; set; }
}

public class BudgetDashboardSummaryDbResult
{
    public int      TotalBudgets         { get; set; }
    public int      ExceededCount        { get; set; }
    public int      NearLimitCount       { get; set; }
    public int      OnTrackCount         { get; set; }
    public byte     OverallHealthScore   { get; set; }
    public decimal  TotalRemainingAmount { get; set; }
    public decimal  TotalBudgetedAmount  { get; set; }
    public decimal  TotalActualSpent     { get; set; }
}

public class BudgetTrendPointDbResult
{
    public DateOnly PeriodStart     { get; set; }
    public decimal  AvgUtilizationPct { get; set; }
    public decimal  TotalBudgeted   { get; set; }
    public decimal  TotalSpent      { get; set; }
    public int      ExceededCount   { get; set; }
    public byte     AvgHealthScore  { get; set; }
}

public class BudgetAnalyticsRowDbResult
{
    public long     BudgetId         { get; set; }
    public string   BudgetName       { get; set; } = null!;
    public int?     CategoryId       { get; set; }
    public string?  CategoryNameEn   { get; set; }
    public string?  CategoryNameAr   { get; set; }
    public byte     PeriodTypeId     { get; set; }
    public long     PeriodId         { get; set; }
    public DateOnly PeriodStart      { get; set; }
    public DateOnly PeriodEnd        { get; set; }
    public decimal  BudgetedAmount   { get; set; }
    public decimal  ActualSpent      { get; set; }
    public decimal  UtilizationPct   { get; set; }
    public decimal  OverBudgetAmount { get; set; }
    public byte     HealthScore      { get; set; }
    public byte     HealthBandId     { get; set; }
    public byte     ForecastRiskId   { get; set; }
    public byte     PeriodStatusId   { get; set; }
    public DateTime? ClosedAtUtc     { get; set; }
}

// ── Background job DB models ───────────────────────────────────────────────────

public class BudgetComputeSnapshotDbResult
{
    public bool  Alert80PctTriggered  { get; set; }
    public bool  Alert100PctTriggered { get; set; }
    public long  PeriodId             { get; set; }
}

public class BudgetActiveUserDbResult
{
    public long UserId { get; set; }
}

public class BudgetForNotificationDbResult
{
    public long   BudgetId    { get; set; }
    public string Name        { get; set; } = null!;
    public decimal BudgetedAmount { get; set; }
    public decimal ActualSpent   { get; set; }
    public decimal UtilizationPct { get; set; }
    public decimal OverBudgetAmount { get; set; }
    public decimal RemainingAmount { get; set; }
}
