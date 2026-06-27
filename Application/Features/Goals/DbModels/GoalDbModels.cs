namespace Application.Features.Goals.DbModels;

// ── Create ────────────────────────────────────────────────────────────────────

public class CreateGoalDbModel
{
    public long     UserId        { get; set; }
    public long?    WorkspaceId   { get; set; }
    public string   Name          { get; set; } = null!;
    public string?  Description   { get; set; }
    public byte     GoalTypeId    { get; set; }
    public decimal  TargetAmount  { get; set; }
    public decimal  InitialAmount { get; set; }
    public DateOnly? TargetDate   { get; set; }
    public byte     Priority      { get; set; }
    public string?  Icon          { get; set; }
    public string?  Color         { get; set; }
}

// ── Get / List ────────────────────────────────────────────────────────────────

public class GoalDbResult
{
    public long      GoalId              { get; set; }
    public string    Name                { get; set; } = null!;
    public string?   Description         { get; set; }
    public byte      GoalTypeId          { get; set; }
    public decimal   TargetAmount        { get; set; }
    public decimal   CurrentAmount       { get; set; }
    public DateOnly? TargetDate          { get; set; }
    public byte      Priority            { get; set; }
    public byte      StatusId            { get; set; }
    public string?   Icon                { get; set; }
    public string?   Color               { get; set; }
    public DateTime  CreatedAtUtc        { get; set; }
    public DateTime? UpdatedAtUtc        { get; set; }
    public DateTime? CompletedAtUtc      { get; set; }
    public int       ContributionCount   { get; set; }
    public int       LinkedRecurringCount { get; set; }
}

public class GoalRowDbResult
{
    public long      GoalId               { get; set; }
    public string    Name                 { get; set; } = null!;
    public string?   Description          { get; set; }
    public byte      GoalTypeId           { get; set; }
    public decimal   TargetAmount         { get; set; }
    public decimal   CurrentAmount        { get; set; }
    public DateOnly? TargetDate           { get; set; }
    public byte      Priority             { get; set; }
    public byte      StatusId             { get; set; }
    public string?   Icon                 { get; set; }
    public string?   Color                { get; set; }
    public DateTime  CreatedAtUtc         { get; set; }
    public DateTime? CompletedAtUtc       { get; set; }
    public decimal   CompletionPercent    { get; set; }
    public DateOnly? LastContributionDate { get; set; }
    public int       LinkedRecurringCount { get; set; }
}

public class GetGoalsDbModel
{
    public long  UserId      { get; set; }
    public long? WorkspaceId { get; set; }
    public byte? StatusId    { get; set; }
    public byte? GoalTypeId { get; set; }
    public byte? Priority   { get; set; }
    public int   PageNumber { get; set; } = 1;
    public int   PageSize   { get; set; } = 20;
}

public class GetGoalsDbResult
{
    public int                          TotalCount { get; set; }
    public IReadOnlyList<GoalRowDbResult> Items    { get; set; } = [];
}

// ── Update ────────────────────────────────────────────────────────────────────

public class UpdateGoalDbModel
{
    public long     GoalId        { get; set; }
    public long     UserId        { get; set; }
    public long?    WorkspaceId   { get; set; }
    public string   Name          { get; set; } = null!;
    public string?  Description   { get; set; }
    public decimal  TargetAmount  { get; set; }
    public DateOnly? TargetDate   { get; set; }
    public byte     Priority      { get; set; }
    public string?  Icon          { get; set; }
    public string?  Color         { get; set; }
}

// ── Contributions ─────────────────────────────────────────────────────────────

public class AddContributionDbModel
{
    public long     GoalId              { get; set; }
    public long     UserId              { get; set; }
    public long?    WorkspaceId         { get; set; }
    public byte     ContributionTypeId  { get; set; }
    public decimal  Amount              { get; set; }
    public bool     IsDebit             { get; set; }
    public string?  Notes               { get; set; }
    public DateOnly ContributionDate    { get; set; }
    public long?    SourceRecurringId   { get; set; }
    public long?    LinkedTransactionId { get; set; }
}

public class AddContributionOutcomeDbResult
{
    public long    ContributionId       { get; set; }
    public decimal NewCurrentAmount     { get; set; }
    public decimal NewCompletionPercent { get; set; }
    public bool    GoalCompleted        { get; set; }
    public int     ErrorCode            { get; set; }
    public string  GoalName             { get; set; } = null!;
}

public class AddContributionDbResult
{
    public long              ContributionId       { get; set; }
    public decimal           NewCurrentAmount     { get; set; }
    public decimal           NewCompletionPercent { get; set; }
    public bool              GoalCompleted        { get; set; }
    public int               ErrorCode            { get; set; }
    public string            GoalName             { get; set; } = null!;
    public IReadOnlyList<byte> NewMilestones      { get; set; } = [];
}

public class GetContributionsDbModel
{
    public long  UserId      { get; set; }
    public long? WorkspaceId { get; set; }
    public long  GoalId      { get; set; }
    public int  PageNumber { get; set; } = 1;
    public int  PageSize   { get; set; } = 20;
}

public class ContributionRowDbResult
{
    public long     ContributionId      { get; set; }
    public byte     ContributionTypeId  { get; set; }
    public decimal  Amount              { get; set; }
    public bool     IsDebit             { get; set; }
    public string?  Notes               { get; set; }
    public DateOnly ContributionDate    { get; set; }
    public DateTime CreatedAtUtc        { get; set; }
    public long?    SourceRecurringId   { get; set; }
    public long?    LinkedTransactionId { get; set; }
}

public class GetContributionsDbResult
{
    public int                                  TotalCount { get; set; }
    public IReadOnlyList<ContributionRowDbResult> Items    { get; set; } = [];
}

public class MonthlyStatsDbResult
{
    public int     Year             { get; set; }
    public int     Month            { get; set; }
    public decimal TotalContributed { get; set; }
    public decimal TotalWithdrawn   { get; set; }
}

// ── Milestones ────────────────────────────────────────────────────────────────

public class MilestoneDbResult
{
    public long      MilestoneId      { get; set; }
    public byte      MilestonePercent { get; set; }
    public DateTime  ReachedAtUtc     { get; set; }
    public DateTime? NotifiedAtUtc    { get; set; }
}

// ── Recurring links ───────────────────────────────────────────────────────────

public class GoalRecurringLinkDbModel
{
    public long  UserId                { get; set; }
    public long? WorkspaceId           { get; set; }
    public long  GoalId                { get; set; }
    public long  RecurringDefinitionId { get; set; }
}

public class GoalRecurringLinkDbResult
{
    public long    LinkId                { get; set; }
    public long    RecurringDefinitionId { get; set; }
    public string  RecurringName         { get; set; } = null!;
    public decimal RecurringAmount       { get; set; }
    public byte    FrequencyId           { get; set; }
    public byte    RecurringStatusId     { get; set; }
    public DateTime CreatedAtUtc         { get; set; }
}

// ── Dashboard ─────────────────────────────────────────────────────────────────

public class GoalDashboardKpiDbResult
{
    public int     ActiveGoalCount    { get; set; }
    public int     PausedGoalCount    { get; set; }
    public int     CompletedGoalCount { get; set; }
    public decimal TotalTargetAmount  { get; set; }
    public decimal TotalSavedAmount   { get; set; }
    public decimal TotalRemainingAmount { get; set; }
}

public class GoalDashboardItemDbResult
{
    public long      GoalId            { get; set; }
    public string    Name              { get; set; } = null!;
    public byte      GoalTypeId        { get; set; }
    public decimal   TargetAmount      { get; set; }
    public decimal   CurrentAmount     { get; set; }
    public DateOnly? TargetDate        { get; set; }
    public byte      Priority          { get; set; }
    public byte      StatusId          { get; set; }
    public string?   Icon              { get; set; }
    public string?   Color             { get; set; }
    public decimal   CompletionPercent { get; set; }
}

public class GoalDashboardDbResult
{
    public GoalDashboardKpiDbResult             Kpi      { get; set; } = null!;
    public IReadOnlyList<GoalDashboardItemDbResult> TopGoals { get; set; } = [];
}

// ── Background jobs ───────────────────────────────────────────────────────────

public class GoalForScheduleCheckDbResult
{
    public long      GoalId            { get; set; }
    public long      UserId            { get; set; }
    public string    Name              { get; set; } = null!;
    public decimal   TargetAmount      { get; set; }
    public decimal   CurrentAmount     { get; set; }
    public DateOnly  TargetDate        { get; set; }
    public decimal   CompletionPercent { get; set; }
    public int       DaysElapsed       { get; set; }
    public int       TotalDays         { get; set; }
}

public class PendingAutoContributionDbResult
{
    public long      GoalId          { get; set; }
    public long      UserId          { get; set; }
    public long      TransactionId   { get; set; }
    public decimal   Amount          { get; set; }
    public DateOnly  TransactionDate { get; set; }
}

public sealed class TotalCountRow { public int TotalCount { get; set; } }
public sealed class MilestonePercentRow { public byte MilestonePercent { get; set; } }
