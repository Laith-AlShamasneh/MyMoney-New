using Application.Features.Goals.DbModels;
using Application.Interfaces.Database;
using Application.Interfaces.Repositories;
using Dapper;
using System.Data;

namespace Infrastructure.Services.Goals;

internal sealed class GoalRepository(IDbExecutor db) : IGoalRepository
{
    // ── Goal CRUD ─────────────────────────────────────────────────────────────

    public async Task<long> CreateAsync(CreateGoalDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",        model.UserId,        DbType.Int64);
        p.Add("@WorkspaceId",   model.WorkspaceId,   DbType.Int64);
        p.Add("@Name",          model.Name,          DbType.String);
        p.Add("@Description",   model.Description,   DbType.String);
        p.Add("@GoalTypeId",    model.GoalTypeId,    DbType.Byte);
        p.Add("@TargetAmount",  model.TargetAmount,  DbType.Decimal);
        p.Add("@InitialAmount", model.InitialAmount, DbType.Decimal);
        p.Add("@TargetDate",    model.TargetDate,    DbType.Date);
        p.Add("@Priority",      model.Priority,      DbType.Byte);
        p.Add("@Icon",          model.Icon,          DbType.String);
        p.Add("@Color",         model.Color,         DbType.String);

        return await db.ExecuteScalarAsync<long>("MyMoney.usp_Goal_Create", p, ct);
    }

    public async Task<GoalDbResult?> GetByIdAsync(long goalId, long userId, long? workspaceId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@GoalId",      goalId,      DbType.Int64);
        p.Add("@UserId",      userId,      DbType.Int64);
        p.Add("@WorkspaceId", workspaceId, DbType.Int64);

        return await db.QuerySingleAsync<GoalDbResult>("MyMoney.usp_Goal_GetById", p, ct);
    }

    public async Task<GetGoalsDbResult> GetListAsync(GetGoalsDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",      model.UserId,      DbType.Int64);
        p.Add("@WorkspaceId", model.WorkspaceId, DbType.Int64);
        p.Add("@StatusId",   model.StatusId,   DbType.Byte);
        p.Add("@GoalTypeId", model.GoalTypeId, DbType.Byte);
        p.Add("@Priority",   model.Priority,   DbType.Byte);
        p.Add("@PageNumber", model.PageNumber, DbType.Int32);
        p.Add("@PageSize",   model.PageSize,   DbType.Int32);

        return await db.QueryMultipleAsync<GetGoalsDbResult>(
            "MyMoney.usp_Goal_GetList",
            async multi =>
            {
                var countRow = await multi.ReadFirstOrDefaultAsync<TotalCountRow>();
                var items    = (await multi.ReadAsync<GoalRowDbResult>()).ToList();
                return new GetGoalsDbResult
                {
                    TotalCount = countRow?.TotalCount ?? 0,
                    Items      = items,
                };
            },
            p, ct);
    }

    public async Task<int> UpdateAsync(UpdateGoalDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@GoalId",       model.GoalId,       DbType.Int64);
        p.Add("@UserId",       model.UserId,        DbType.Int64);
        p.Add("@WorkspaceId",  model.WorkspaceId,   DbType.Int64);
        p.Add("@Name",         model.Name,          DbType.String);
        p.Add("@Description",  model.Description,   DbType.String);
        p.Add("@TargetAmount", model.TargetAmount,  DbType.Decimal);
        p.Add("@TargetDate",   model.TargetDate,    DbType.Date);
        p.Add("@Priority",     model.Priority,      DbType.Byte);
        p.Add("@Icon",         model.Icon,          DbType.String);
        p.Add("@Color",        model.Color,         DbType.String);

        return await db.ExecuteScalarAsync<int>("MyMoney.usp_Goal_Update", p, ct);
    }

    public async Task<int> SetStatusAsync(long goalId, long userId, long? workspaceId, byte statusId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@GoalId",      goalId,      DbType.Int64);
        p.Add("@UserId",      userId,      DbType.Int64);
        p.Add("@WorkspaceId", workspaceId, DbType.Int64);
        p.Add("@StatusId", statusId, DbType.Byte);

        return await db.ExecuteScalarAsync<int>("MyMoney.usp_Goal_SetStatus", p, ct);
    }

    // ── Contributions ─────────────────────────────────────────────────────────

    public async Task<AddContributionDbResult> AddContributionAsync(
        AddContributionDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@GoalId",              model.GoalId,              DbType.Int64);
        p.Add("@UserId",              model.UserId,              DbType.Int64);
        p.Add("@WorkspaceId",         model.WorkspaceId,         DbType.Int64);
        p.Add("@ContributionTypeId",  model.ContributionTypeId,  DbType.Byte);
        p.Add("@Amount",              model.Amount,              DbType.Decimal);
        p.Add("@IsDebit",             model.IsDebit,             DbType.Boolean);
        p.Add("@Notes",               model.Notes,               DbType.String);
        p.Add("@ContributionDate",    model.ContributionDate,    DbType.Date);
        p.Add("@SourceRecurringId",   model.SourceRecurringId,   DbType.Int64);
        p.Add("@LinkedTransactionId", model.LinkedTransactionId, DbType.Int64);

        return await db.QueryMultipleAsync<AddContributionDbResult>(
            "MyMoney.usp_GoalContribution_Add",
            async multi =>
            {
                var outcome    = await multi.ReadFirstOrDefaultAsync<AddContributionOutcomeDbResult>();
                var milestones = (await multi.ReadAsync<MilestonePercentRow>())
                                    .Select(r => r.MilestonePercent).ToList();

                if (outcome is null)
                {
                    return new AddContributionDbResult { ErrorCode = -1 };
                }

                return new AddContributionDbResult
                {
                    ContributionId       = outcome.ContributionId,
                    NewCurrentAmount     = outcome.NewCurrentAmount,
                    NewCompletionPercent = outcome.NewCompletionPercent,
                    GoalCompleted        = outcome.GoalCompleted,
                    ErrorCode            = outcome.ErrorCode,
                    GoalName             = outcome.GoalName,
                    NewMilestones        = milestones,
                };
            },
            p, ct);
    }

    public async Task<GetContributionsDbResult> GetContributionsAsync(
        GetContributionsDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@GoalId",      model.GoalId,      DbType.Int64);
        p.Add("@UserId",      model.UserId,      DbType.Int64);
        p.Add("@WorkspaceId", model.WorkspaceId, DbType.Int64);
        p.Add("@PageNumber", model.PageNumber, DbType.Int32);
        p.Add("@PageSize",   model.PageSize,   DbType.Int32);

        return await db.QueryMultipleAsync<GetContributionsDbResult>(
            "MyMoney.usp_GoalContribution_GetList",
            async multi =>
            {
                var countRow = await multi.ReadFirstOrDefaultAsync<TotalCountRow>();
                var items    = (await multi.ReadAsync<ContributionRowDbResult>()).ToList();
                return new GetContributionsDbResult
                {
                    TotalCount = countRow?.TotalCount ?? 0,
                    Items      = items,
                };
            },
            p, ct);
    }

    public async Task<IReadOnlyList<MonthlyStatsDbResult>> GetMonthlyStatsAsync(
        long goalId, long userId, long? workspaceId, int monthsBack = 3, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@GoalId",      goalId,      DbType.Int64);
        p.Add("@UserId",      userId,      DbType.Int64);
        p.Add("@WorkspaceId", workspaceId, DbType.Int64);
        p.Add("@MonthsBack", monthsBack, DbType.Int32);

        return await db.QueryListAsync<MonthlyStatsDbResult>(
            "MyMoney.usp_GoalContribution_GetMonthlyStats", p, ct);
    }

    // ── Milestones ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<MilestoneDbResult>> GetMilestonesAsync(
        long goalId, long userId, long? workspaceId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@GoalId",      goalId,      DbType.Int64);
        p.Add("@UserId",      userId,      DbType.Int64);
        p.Add("@WorkspaceId", workspaceId, DbType.Int64);

        return await db.QueryListAsync<MilestoneDbResult>(
            "MyMoney.usp_GoalMilestone_GetByGoal", p, ct);
    }

    public async Task MarkMilestoneNotifiedAsync(long goalId, byte milestonePercent, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@GoalId",           goalId,          DbType.Int64);
        p.Add("@MilestonePercent", milestonePercent, DbType.Byte);

        await db.ExecuteAsync("MyMoney.usp_GoalMilestone_MarkNotified", p, ct);
    }

    // ── Recurring links ───────────────────────────────────────────────────────

    public async Task<bool> UpsertRecurringLinkAsync(
        GoalRecurringLinkDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@GoalId",                model.GoalId,                DbType.Int64);
        p.Add("@UserId",                model.UserId,                DbType.Int64);
        p.Add("@WorkspaceId",           model.WorkspaceId,           DbType.Int64);
        p.Add("@RecurringDefinitionId", model.RecurringDefinitionId, DbType.Int64);

        return await db.ExecuteScalarAsync<bool>("MyMoney.usp_GoalRecurringLink_Upsert", p, ct);
    }

    public async Task<int> DeleteRecurringLinkAsync(
        long goalId, long userId, long? workspaceId, long recurringDefinitionId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@GoalId",                goalId,                DbType.Int64);
        p.Add("@UserId",                userId,                DbType.Int64);
        p.Add("@WorkspaceId",           workspaceId,           DbType.Int64);
        p.Add("@RecurringDefinitionId", recurringDefinitionId, DbType.Int64);

        return await db.ExecuteScalarAsync<int>("MyMoney.usp_GoalRecurringLink_Delete", p, ct);
    }

    public async Task<IReadOnlyList<GoalRecurringLinkDbResult>> GetRecurringLinksAsync(
        long goalId, long userId, long? workspaceId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@GoalId",      goalId,      DbType.Int64);
        p.Add("@UserId",      userId,      DbType.Int64);
        p.Add("@WorkspaceId", workspaceId, DbType.Int64);

        return await db.QueryListAsync<GoalRecurringLinkDbResult>(
            "MyMoney.usp_GoalRecurringLink_GetByGoal", p, ct);
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    public async Task<GoalDashboardDbResult> GetDashboardAsync(long userId, long? workspaceId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",      userId,      DbType.Int64);
        p.Add("@WorkspaceId", workspaceId, DbType.Int64);

        return await db.QueryMultipleAsync<GoalDashboardDbResult>(
            "MyMoney.usp_Goal_GetDashboard",
            async multi =>
            {
                var kpi      = await multi.ReadFirstOrDefaultAsync<GoalDashboardKpiDbResult>()
                               ?? new GoalDashboardKpiDbResult();
                var topGoals = (await multi.ReadAsync<GoalDashboardItemDbResult>()).ToList();
                return new GoalDashboardDbResult
                {
                    Kpi      = kpi,
                    TopGoals = topGoals,
                };
            },
            p, ct);
    }

    // ── Background jobs ───────────────────────────────────────────────────────

    public async Task<IReadOnlyList<GoalForScheduleCheckDbResult>> GetActiveGoalsForScheduleCheckAsync(
        CancellationToken ct = default)
    {
        return await db.QueryListAsync<GoalForScheduleCheckDbResult>(
            "MyMoney.usp_Goal_GetActiveForScheduleCheck", null, ct);
    }

    public async Task<IReadOnlyList<PendingAutoContributionDbResult>> GetPendingAutoContributionsAsync(
        DateOnly date, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@Date", date, DbType.Date);

        return await db.QueryListAsync<PendingAutoContributionDbResult>(
            "MyMoney.usp_Goal_GetPendingAutoContributions", p, ct);
    }

    public async Task UpdateBehindScheduleNotifiedAsync(long goalId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@GoalId", goalId, DbType.Int64);

        await db.ExecuteAsync("MyMoney.usp_Goal_UpdateBehindScheduleNotified", p, ct);
    }
}
