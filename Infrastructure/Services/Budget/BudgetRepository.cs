using Application.Features.Budget.DbModels;
using Application.Interfaces.Database;
using Application.Interfaces.Repositories;
using Dapper;
using System.Data;

namespace Infrastructure.Services.Budget;

internal sealed class BudgetRepository(IDbExecutor db) : IBudgetRepository
{
    // ── Create ─────────────────────────────────────────────────────────────────

    public async Task<CreateBudgetDbResult> CreateAsync(CreateBudgetDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",       model.UserId,       DbType.Int64);
        p.Add("@WorkspaceId",  model.WorkspaceId,  DbType.Int64);
        p.Add("@Name",         model.Name,         DbType.String);
        p.Add("@CategoryId",   model.CategoryId,   DbType.Int32);
        p.Add("@BudgetTypeId", model.BudgetTypeId, DbType.Byte);
        p.Add("@Amount",       model.Amount,       DbType.Decimal);
        p.Add("@PeriodTypeId", model.PeriodTypeId, DbType.Byte);
        p.Add("@StartDate",    model.StartDate,    DbType.Date);
        p.Add("@EndDate",      model.EndDate,      DbType.Date);
        p.Add("@IsAutoRenew",  model.IsAutoRenew,  DbType.Boolean);
        p.Add("@Notes",        model.Notes,        DbType.String);
        p.Add("@NewBudgetId",  dbType: DbType.Int64,  direction: ParameterDirection.Output);
        p.Add("@ResultCode",   dbType: DbType.Byte,   direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Budget_Create", p, ct);

        return new CreateBudgetDbResult
        {
            NewBudgetId = p.Get<long>("@NewBudgetId"),
            ResultCode  = p.Get<byte>("@ResultCode"),
        };
    }

    // ── Update ─────────────────────────────────────────────────────────────────

    public async Task<int> UpdateAsync(UpdateBudgetDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",      model.UserId,      DbType.Int64);
        p.Add("@WorkspaceId", model.WorkspaceId, DbType.Int64);
        p.Add("@BudgetId",    model.BudgetId,    DbType.Int64);
        p.Add("@Name",        model.Name,        DbType.String);
        p.Add("@Amount",      model.Amount,      DbType.Decimal);
        p.Add("@EndDate",     model.EndDate,     DbType.Date);
        p.Add("@IsAutoRenew", model.IsAutoRenew, DbType.Boolean);
        p.Add("@Notes",       model.Notes,       DbType.String);
        p.Add("@AffectedRows", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Budget_Update", p, ct);

        return p.Get<int>("@AffectedRows");
    }

    // ── Update Status ──────────────────────────────────────────────────────────

    public async Task<int> UpdateStatusAsync(UpdateBudgetStatusDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",      model.UserId,      DbType.Int64);
        p.Add("@WorkspaceId", model.WorkspaceId, DbType.Int64);
        p.Add("@BudgetId",    model.BudgetId,    DbType.Int64);
        p.Add("@NewStatusId", model.NewStatusId, DbType.Byte);
        p.Add("@AffectedRows", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Budget_UpdateStatus", p, ct);

        return p.Get<int>("@AffectedRows");
    }

    // ── Delete ─────────────────────────────────────────────────────────────────

    public async Task<int> DeleteAsync(DeleteBudgetDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",      model.UserId,      DbType.Int64);
        p.Add("@WorkspaceId", model.WorkspaceId, DbType.Int64);
        p.Add("@BudgetId",    model.BudgetId,    DbType.Int64);
        p.Add("@AffectedRows", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Budget_Delete", p, ct);

        return p.Get<int>("@AffectedRows");
    }

    // ── Get List ───────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<BudgetRowDbResult>> GetListAsync(GetBudgetListDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",      model.UserId,      DbType.Int64);
        p.Add("@WorkspaceId", model.WorkspaceId, DbType.Int64);
        p.Add("@StatusId",    model.StatusId,    DbType.Byte);

        return await db.QueryListAsync<BudgetRowDbResult>("MyMoney.usp_Budget_GetList", p, ct);
    }

    // ── Get By ID ──────────────────────────────────────────────────────────────

    public async Task<(BudgetDetailDbResult? Budget, IReadOnlyList<BudgetPeriodRowDbResult> History)> GetByIdAsync(
        long userId, long? workspaceId, long budgetId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",      userId,      DbType.Int64);
        p.Add("@WorkspaceId", workspaceId, DbType.Int64);
        p.Add("@BudgetId",    budgetId,    DbType.Int64);

        return await db.QueryMultipleAsync(
            "MyMoney.usp_Budget_GetById",
            async multi =>
            {
                var budget  = await multi.ReadFirstOrDefaultAsync<BudgetDetailDbResult>();
                var history = (await multi.ReadAsync<BudgetPeriodRowDbResult>()).ToList();
                return (budget, (IReadOnlyList<BudgetPeriodRowDbResult>)history);
            },
            p, ct);
    }

    // ── Dashboard ──────────────────────────────────────────────────────────────

    public async Task<(BudgetDashboardSummaryDbResult? Summary, IReadOnlyList<BudgetRowDbResult> Budgets, IReadOnlyList<BudgetTrendPointDbResult> Trend)>
        GetDashboardAsync(long userId, long? workspaceId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",      userId,      DbType.Int64);
        p.Add("@WorkspaceId", workspaceId, DbType.Int64);

        return await db.QueryMultipleAsync(
            "MyMoney.usp_Budget_GetDashboard",
            async multi =>
            {
                var summary = await multi.ReadFirstOrDefaultAsync<BudgetDashboardSummaryDbResult>();
                var budgets = (await multi.ReadAsync<BudgetRowDbResult>()).ToList();
                var trend   = (await multi.ReadAsync<BudgetTrendPointDbResult>()).ToList();
                return (summary, (IReadOnlyList<BudgetRowDbResult>)budgets, (IReadOnlyList<BudgetTrendPointDbResult>)trend);
            },
            p, ct);
    }

    // ── Analytics ──────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<BudgetAnalyticsRowDbResult>> GetAnalyticsAsync(GetBudgetAnalyticsDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",      model.UserId,      DbType.Int64);
        p.Add("@WorkspaceId", model.WorkspaceId, DbType.Int64);
        p.Add("@BudgetId",    model.BudgetId,    DbType.Int64);
        p.Add("@DateFrom",    model.DateFrom,    DbType.Date);
        p.Add("@DateTo",      model.DateTo,      DbType.Date);

        return await db.QueryListAsync<BudgetAnalyticsRowDbResult>("MyMoney.usp_Budget_GetAnalytics", p, ct);
    }

    // ── Period History ─────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<BudgetPeriodRowDbResult>> GetPeriodsAsync(GetBudgetPeriodsDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",      model.UserId,      DbType.Int64);
        p.Add("@WorkspaceId", model.WorkspaceId, DbType.Int64);
        p.Add("@BudgetId",    model.BudgetId,    DbType.Int64);
        p.Add("@PageNumber",  model.PageNumber,  DbType.Int32);
        p.Add("@PageSize",    model.PageSize,    DbType.Int32);

        return await db.QueryListAsync<BudgetPeriodRowDbResult>("MyMoney.usp_BudgetPeriod_GetList", p, ct);
    }

    // ── Background: Compute Snapshot ──────────────────────────────────────────

    public async Task<BudgetComputeSnapshotDbResult> ComputeSnapshotAsync(long userId, long budgetId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",               userId,   DbType.Int64);
        p.Add("@BudgetId",             budgetId, DbType.Int64);
        p.Add("@Alert80PctTriggered",  dbType: DbType.Boolean, direction: ParameterDirection.Output);
        p.Add("@Alert100PctTriggered", dbType: DbType.Boolean, direction: ParameterDirection.Output);
        p.Add("@PeriodId",             dbType: DbType.Int64,   direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_BudgetPeriod_ComputeSnapshot", p, ct);

        return new BudgetComputeSnapshotDbResult
        {
            Alert80PctTriggered  = p.Get<bool>("@Alert80PctTriggered"),
            Alert100PctTriggered = p.Get<bool>("@Alert100PctTriggered"),
            PeriodId             = p.Get<long>("@PeriodId"),
        };
    }

    // ── Background: Close Expired Periods ─────────────────────────────────────

    public async Task<(int ClosedCount, int CreatedCount)> CloseExpiredPeriodsAsync(long? userId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",              userId, DbType.Int64);
        p.Add("@PeriodsClosedCount",  dbType: DbType.Int32, direction: ParameterDirection.Output);
        p.Add("@PeriodsCreatedCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_BudgetPeriod_CloseExpired", p, ct);

        return (p.Get<int>("@PeriodsClosedCount"), p.Get<int>("@PeriodsCreatedCount"));
    }

    // ── Background: Active Users ───────────────────────────────────────────────

    public async Task<IReadOnlyList<BudgetActiveUserDbResult>> GetActiveUserIdsAsync(CancellationToken ct = default)
    {
        return await db.QueryListAsync<BudgetActiveUserDbResult>("MyMoney.usp_Budget_GetActiveUserIds", null, ct);
    }

    // ── Background: Active Budgets For User ───────────────────────────────────

    public async Task<IReadOnlyList<BudgetRowDbResult>> GetActiveBudgetsForUserAsync(long userId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",   userId, DbType.Int64);
        p.Add("@StatusId", (byte)1, DbType.Byte);  // Active only

        return await db.QueryListAsync<BudgetRowDbResult>("MyMoney.usp_Budget_GetList", p, ct);
    }
}
