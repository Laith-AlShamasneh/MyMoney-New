namespace Application.Features.CashFlow.DbModels;

// ── Computation inputs (from usp_CashFlow_GetComputationInputs) ───────────────

public class MonthlySnapshotInput
{
    public DateOnly SnapshotDate     { get; set; }
    public decimal  TotalIncome      { get; set; }
    public decimal  TotalExpense     { get; set; }
    public decimal  NetBalance       { get; set; }
    public decimal  AverageDailySpend { get; set; }
    public int      TransactionCount { get; set; }
}

public class RecurringDefinitionInput
{
    public long      RecurringId       { get; set; }
    public byte      TransactionTypeId { get; set; }
    public decimal   Amount            { get; set; }
    public byte      FrequencyId       { get; set; }
    public int?      FrequencyInterval { get; set; }
    public byte?     FrequencyUnit     { get; set; }
    public byte?     DayOfMonth        { get; set; }
    public byte?     DayOfWeek         { get; set; }
    public DateOnly  StartDate         { get; set; }
    public DateOnly? EndDate           { get; set; }
    public bool      IsSubscription    { get; set; }
    public string?   ProviderName      { get; set; }
    public DateOnly? RenewalDate       { get; set; }
    public bool      AutoRenew         { get; set; }
}

public class ActiveGoalInput
{
    public long      GoalId          { get; set; }
    public string    GoalName        { get; set; } = null!;
    public decimal   TargetAmount    { get; set; }
    public decimal   CurrentAmount   { get; set; }
    public DateOnly? TargetDate      { get; set; }
    public decimal   AvgMonthlyPace  { get; set; }
}

public class CategoryTrendInput
{
    public int     CategoryId       { get; set; }
    public byte    TrendDirection   { get; set; }
    public decimal ChangePercentage { get; set; }
}

// ── Engine data structures ────────────────────────────────────────────────────

public class ForecastComputationInputs
{
    public IReadOnlyList<MonthlySnapshotInput>    MonthlySnapshots  { get; set; } = [];
    public IReadOnlyList<RecurringDefinitionInput> ActiveRecurringDefs { get; set; } = [];
    public IReadOnlyList<ActiveGoalInput>         ActiveGoals       { get; set; } = [];
    public IReadOnlyList<CategoryTrendInput>      CategoryTrends    { get; set; } = [];
    public decimal                                CurrentBalanceEst { get; set; }
}

public class ForecastMonthlyPointData
{
    public DateOnly MonthYear        { get; set; }
    public decimal  ProjectedIncome  { get; set; }
    public decimal  ProjectedExpense { get; set; }
    public decimal  ProjectedNet     { get; set; }
    public decimal  RunningBalance   { get; set; }
    public decimal  RecurringIncome  { get; set; }
    public decimal  RecurringExpense { get; set; }
    public decimal  VariableIncome   { get; set; }
    public decimal  VariableExpense  { get; set; }
    public decimal  ConfidenceScore  { get; set; }
}

public class ForecastGoalProjectionData
{
    public long      GoalId               { get; set; }
    public string    GoalName             { get; set; } = null!;
    public decimal   TargetAmount         { get; set; }
    public decimal   CurrentAmount        { get; set; }
    public DateOnly? TargetDate           { get; set; }
    public decimal   RequiredMonthlyContr { get; set; }
    public decimal   AvgMonthlyPace       { get; set; }
    public DateOnly? EstimatedComplDate   { get; set; }
    public bool      IsAtRisk             { get; set; }
    public int?      DaysToCompletion     { get; set; }
}

public class ForecastRiskData
{
    public byte     RiskType          { get; set; }
    public byte     Severity          { get; set; }
    public string   TitleEn           { get; set; } = null!;
    public string   TitleAr           { get; set; } = null!;
    public string   DescriptionEn     { get; set; } = null!;
    public string   DescriptionAr     { get; set; } = null!;
    public DateOnly? AffectedMonthYear { get; set; }
    public string?  DataPointJson     { get; set; }
}

public class ForecastComputationResult
{
    public int                                       MonthsOfHistoryUsed   { get; set; }
    public decimal                                   CurrentBalanceEst     { get; set; }
    public decimal                                   OverallConfidence     { get; set; }
    public byte                                      ConfidenceBand        { get; set; }
    public decimal                                   RecurringIncomeMonthly { get; set; }
    public decimal                                   RecurringExpMonthly   { get; set; }
    public decimal                                   AvgVarIncomeMonthly   { get; set; }
    public decimal                                   AvgVarExpMonthly      { get; set; }
    public decimal                                   ForecastedEndBalance  { get; set; }
    public List<ForecastMonthlyPointData>            MonthlyPoints         { get; set; } = [];
    public List<ForecastGoalProjectionData>          GoalProjections       { get; set; } = [];
    public List<ForecastRiskData>                    Risks                 { get; set; } = [];
}

// ── Repository read models (from usp_CashFlow_GetForecast) ────────────────────

public class ForecastHeaderDbResult
{
    public long     ForecastId           { get; set; }
    public DateTime GeneratedAtUtc       { get; set; }
    public byte     HorizonMonths        { get; set; }
    public byte     MonthsOfHistoryUsed  { get; set; }
    public decimal  CurrentBalanceEst    { get; set; }
    public decimal  OverallConfidence    { get; set; }
    public byte     ConfidenceBand       { get; set; }
    public decimal  RecurringIncomeMthly { get; set; }
    public decimal  RecurringExpMthly    { get; set; }
    public decimal  AvgVarIncomeMthly    { get; set; }
    public decimal  AvgVarExpMthly       { get; set; }
    public decimal  ForecastedEndBalance { get; set; }
}

public class ForecastPointDbResult
{
    public long     PointId          { get; set; }
    public DateOnly MonthYear        { get; set; }
    public decimal  ProjectedIncome  { get; set; }
    public decimal  ProjectedExpense { get; set; }
    public decimal  ProjectedNet     { get; set; }
    public decimal  RunningBalance   { get; set; }
    public decimal  RecurringIncome  { get; set; }
    public decimal  RecurringExpense { get; set; }
    public decimal  VariableIncome   { get; set; }
    public decimal  VariableExpense  { get; set; }
    public decimal  ConfidenceScore  { get; set; }
}

public class ForecastRiskDbResult
{
    public long     RiskId            { get; set; }
    public byte     RiskType          { get; set; }
    public byte     Severity          { get; set; }
    public string   TitleEn           { get; set; } = null!;
    public string   TitleAr           { get; set; } = null!;
    public string   DescriptionEn     { get; set; } = null!;
    public string   DescriptionAr     { get; set; } = null!;
    public DateOnly? AffectedMonthYear { get; set; }
    public string?  DataPointJson     { get; set; }
}

public class ForecastGoalProjectionDbResult
{
    public long     ProjectionId         { get; set; }
    public long     GoalId               { get; set; }
    public string   GoalName             { get; set; } = null!;
    public decimal  TargetAmount         { get; set; }
    public decimal  CurrentAmount        { get; set; }
    public DateOnly? TargetDate          { get; set; }
    public decimal  RequiredMonthlyContr { get; set; }
    public decimal  AvgMonthlyPace       { get; set; }
    public DateOnly? EstimatedComplDate  { get; set; }
    public bool     IsAtRisk             { get; set; }
    public int?     DaysToCompletion     { get; set; }
}

public class ForecastFullDbResult
{
    public ForecastHeaderDbResult?                   Header          { get; set; }
    public IReadOnlyList<ForecastPointDbResult>      MonthlyPoints   { get; set; } = [];
    public IReadOnlyList<ForecastRiskDbResult>       Risks           { get; set; } = [];
    public IReadOnlyList<ForecastGoalProjectionDbResult> GoalProjections { get; set; } = [];
}

// ── Dashboard read models (from usp_CashFlow_GetDashboard) ───────────────────

public class ForecastDashboardSummaryDbResult
{
    public long     ForecastId           { get; set; }
    public DateTime GeneratedAtUtc       { get; set; }
    public decimal  OverallConfidence    { get; set; }
    public byte     ConfidenceBand       { get; set; }
    public decimal  CurrentBalanceEst    { get; set; }
    public decimal  ForecastedEndBalance { get; set; }
    public byte     MonthsOfHistoryUsed  { get; set; }
    public decimal  RecurringIncomeMthly { get; set; }
    public decimal  RecurringExpMthly    { get; set; }
}

public class ForecastDashboardPointDbResult
{
    public DateOnly MonthYear        { get; set; }
    public decimal  ProjectedIncome  { get; set; }
    public decimal  ProjectedExpense { get; set; }
    public decimal  ProjectedNet     { get; set; }
    public decimal  RunningBalance   { get; set; }
    public decimal  ConfidenceScore  { get; set; }
}

public class ForecastDashboardRiskDbResult
{
    public long     RiskId            { get; set; }
    public byte     RiskType          { get; set; }
    public byte     Severity          { get; set; }
    public string   TitleEn           { get; set; } = null!;
    public string   TitleAr           { get; set; } = null!;
    public string   DescriptionEn     { get; set; } = null!;
    public string   DescriptionAr     { get; set; } = null!;
    public DateOnly? AffectedMonthYear { get; set; }
}

public class ForecastDashboardDbResult
{
    public ForecastDashboardSummaryDbResult?              Summary  { get; set; }
    public IReadOnlyList<ForecastDashboardPointDbResult>  Points   { get; set; } = [];
    public IReadOnlyList<ForecastDashboardRiskDbResult>   Risks    { get; set; } = [];
}

// ── Unnotified risks ──────────────────────────────────────────────────────────

public class UnnotifiedRiskDbResult
{
    public long      RiskId            { get; set; }
    public byte      RiskType          { get; set; }
    public byte      Severity          { get; set; }
    public string    TitleEn           { get; set; } = null!;
    public string    TitleAr           { get; set; } = null!;
    public string    DescriptionEn     { get; set; } = null!;
    public string    DescriptionAr     { get; set; } = null!;
    public DateOnly? AffectedMonthYear { get; set; }
    public string?   DataPointJson     { get; set; }
}

// ── Active users (for scheduler) ─────────────────────────────────────────────

public class CashFlowActiveUserDbResult
{
    public long UserId { get; set; }
}
