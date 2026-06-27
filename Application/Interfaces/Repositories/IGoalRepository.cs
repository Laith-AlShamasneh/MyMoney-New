using Application.Features.Goals.DbModels;

namespace Application.Interfaces.Repositories;

public interface IGoalRepository
{
    // Goals
    Task<long>               CreateAsync(CreateGoalDbModel model, CancellationToken ct = default);
    Task<GoalDbResult?>      GetByIdAsync(long goalId, long userId, long? workspaceId, CancellationToken ct = default);
    Task<GetGoalsDbResult>   GetListAsync(GetGoalsDbModel model, CancellationToken ct = default);
    Task<int>                UpdateAsync(UpdateGoalDbModel model, CancellationToken ct = default);
    Task<int>                SetStatusAsync(long goalId, long userId, long? workspaceId, byte statusId, CancellationToken ct = default);

    // Contributions
    Task<AddContributionDbResult>      AddContributionAsync(AddContributionDbModel model, CancellationToken ct = default);
    Task<GetContributionsDbResult>     GetContributionsAsync(GetContributionsDbModel model, CancellationToken ct = default);
    Task<IReadOnlyList<MonthlyStatsDbResult>> GetMonthlyStatsAsync(long goalId, long userId, long? workspaceId, int monthsBack = 3, CancellationToken ct = default);

    // Milestones
    Task<IReadOnlyList<MilestoneDbResult>> GetMilestonesAsync(long goalId, long userId, long? workspaceId, CancellationToken ct = default);
    Task                                   MarkMilestoneNotifiedAsync(long goalId, byte milestonePercent, CancellationToken ct = default);

    // Recurring links
    Task<bool>                                       UpsertRecurringLinkAsync(GoalRecurringLinkDbModel model, CancellationToken ct = default);
    Task<int>                                        DeleteRecurringLinkAsync(long goalId, long userId, long? workspaceId, long recurringDefinitionId, CancellationToken ct = default);
    Task<IReadOnlyList<GoalRecurringLinkDbResult>>   GetRecurringLinksAsync(long goalId, long userId, long? workspaceId, CancellationToken ct = default);

    // Dashboard
    Task<GoalDashboardDbResult> GetDashboardAsync(long userId, long? workspaceId, CancellationToken ct = default);

    // Background jobs
    Task<IReadOnlyList<GoalForScheduleCheckDbResult>>     GetActiveGoalsForScheduleCheckAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PendingAutoContributionDbResult>>  GetPendingAutoContributionsAsync(DateOnly date, CancellationToken ct = default);
    Task                                                  UpdateBehindScheduleNotifiedAsync(long goalId, CancellationToken ct = default);
}
