namespace Application.Features.Goals.DTOs;

// ── Requests ──────────────────────────────────────────────────────────────────

public sealed record CreateGoalRequest(
    string   Name,
    string?  Description,
    int      GoalTypeId,
    decimal  TargetAmount,
    decimal? InitialAmount,
    string?  TargetDate,
    int?     Priority,
    string?  Icon,
    string?  Color);

public sealed record UpdateGoalRequest(
    long     Id,
    string   Name,
    string?  Description,
    decimal  TargetAmount,
    string?  TargetDate,
    int      Priority,
    string?  Icon,
    string?  Color);

public sealed record GetGoalRequest(long Id);

public sealed record GetGoalListRequest(
    int?  StatusId,
    int?  GoalTypeId,
    int?  Priority,
    int   PageNumber = 1,
    int   PageSize   = 20);

public sealed record DeleteGoalRequest(long Id);
public sealed record PauseGoalRequest(long Id);
public sealed record ResumeGoalRequest(long Id);

public sealed record AddContributionRequest(
    long    GoalId,
    decimal Amount,
    string? Notes,
    string  ContributionDate);

public sealed record WithdrawRequest(
    long    GoalId,
    decimal Amount,
    string? Notes,
    string  ContributionDate);

public sealed record AdjustGoalRequest(
    long    GoalId,
    decimal NewAmount,
    string? Notes,
    string  AdjustmentDate);

public sealed record GetContributionsRequest(
    long GoalId,
    int  PageNumber = 1,
    int  PageSize   = 20);

public sealed record LinkRecurringRequest(
    long GoalId,
    long RecurringDefinitionId);

public sealed record UnlinkRecurringRequest(
    long GoalId,
    long RecurringDefinitionId);

// ── Responses ─────────────────────────────────────────────────────────────────

public sealed record CreateGoalResponse(long GoalId);

public sealed record GoalProgress(
    decimal  CompletionPercent,
    decimal  SavedAmount,
    decimal  RemainingAmount,
    decimal? AvgMonthlyContribution,
    string?  EstimatedCompletionDate,
    bool?    OnTrack,
    decimal? MonthlySavingsNeeded);

public sealed record MilestoneDto(
    byte     MilestonePercent,
    string   ReachedAt,
    bool     IsNotified);

public sealed record RecurringLinkDto(
    long    LinkId,
    long    RecurringDefinitionId,
    string  RecurringName,
    decimal RecurringAmount,
    byte    FrequencyId,
    byte    RecurringStatusId,
    string  LinkedAt);

public sealed record GoalDetailResponse(
    long     GoalId,
    string   Name,
    string?  Description,
    byte     GoalTypeId,
    decimal  TargetAmount,
    decimal  CurrentAmount,
    string?  TargetDate,
    byte     Priority,
    byte     StatusId,
    string?  Icon,
    string?  Color,
    string   CreatedAt,
    string?  CompletedAt,
    int      ContributionCount,
    int      LinkedRecurringCount,
    GoalProgress             Progress,
    IReadOnlyList<MilestoneDto>      Milestones,
    IReadOnlyList<RecurringLinkDto>  RecurringLinks);

public sealed record GoalListItemDto(
    long     GoalId,
    string   Name,
    string?  Description,
    byte     GoalTypeId,
    decimal  TargetAmount,
    decimal  CurrentAmount,
    string?  TargetDate,
    byte     Priority,
    byte     StatusId,
    string?  Icon,
    string?  Color,
    string   CreatedAt,
    string?  CompletedAt,
    decimal  CompletionPercent,
    string?  LastContributionDate,
    int      LinkedRecurringCount);

public sealed record GoalListResponse(
    IReadOnlyList<GoalListItemDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public sealed record AddContributionResponse(
    long    ContributionId,
    decimal NewCurrentAmount,
    decimal NewCompletionPercent,
    bool    GoalCompleted);

public sealed record ContributionDto(
    long    ContributionId,
    byte    ContributionTypeId,
    decimal Amount,
    bool    IsDebit,
    string? Notes,
    string  ContributionDate,
    string  CreatedAt,
    long?   SourceRecurringId,
    long?   LinkedTransactionId);

public sealed record ContributionListResponse(
    IReadOnlyList<ContributionDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public sealed record GoalDashboardKpi(
    int     ActiveGoalCount,
    int     PausedGoalCount,
    int     CompletedGoalCount,
    decimal TotalTargetAmount,
    decimal TotalSavedAmount,
    decimal TotalRemainingAmount);

public sealed record GoalDashboardItemDto(
    long     GoalId,
    string   Name,
    byte     GoalTypeId,
    decimal  TargetAmount,
    decimal  CurrentAmount,
    string?  TargetDate,
    byte     Priority,
    string?  Icon,
    string?  Color,
    decimal  CompletionPercent);

public sealed record GoalDashboardResponse(
    GoalDashboardKpi                      Kpi,
    IReadOnlyList<GoalDashboardItemDto>   TopGoals);
