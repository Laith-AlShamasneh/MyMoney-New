using Application.Features.Budget.DbModels;

namespace Application.Interfaces.Repositories;

public interface IBudgetRepository
{
    // ── CRUD ──────────────────────────────────────────────────────────────────
    Task<CreateBudgetDbResult>  CreateAsync(CreateBudgetDbModel model, CancellationToken ct = default);
    Task<int>                   UpdateAsync(UpdateBudgetDbModel model, CancellationToken ct = default);
    Task<int>                   UpdateStatusAsync(UpdateBudgetStatusDbModel model, CancellationToken ct = default);
    Task<int>                   DeleteAsync(DeleteBudgetDbModel model, CancellationToken ct = default);

    // ── Read ──────────────────────────────────────────────────────────────────
    Task<IReadOnlyList<BudgetRowDbResult>> GetListAsync(GetBudgetListDbModel model, CancellationToken ct = default);
    Task<(BudgetDetailDbResult? Budget, IReadOnlyList<BudgetPeriodRowDbResult> History)> GetByIdAsync(long userId, long? workspaceId, long budgetId, CancellationToken ct = default);
    Task<(BudgetDashboardSummaryDbResult? Summary, IReadOnlyList<BudgetRowDbResult> Budgets, IReadOnlyList<BudgetTrendPointDbResult> Trend)> GetDashboardAsync(long userId, long? workspaceId, CancellationToken ct = default);
    Task<IReadOnlyList<BudgetAnalyticsRowDbResult>> GetAnalyticsAsync(GetBudgetAnalyticsDbModel model, CancellationToken ct = default);

    // ── Period history ────────────────────────────────────────────────────────
    Task<IReadOnlyList<BudgetPeriodRowDbResult>> GetPeriodsAsync(GetBudgetPeriodsDbModel model, CancellationToken ct = default);

    // ── Background jobs ───────────────────────────────────────────────────────
    Task<BudgetComputeSnapshotDbResult> ComputeSnapshotAsync(long userId, long budgetId, CancellationToken ct = default);
    Task<(int ClosedCount, int CreatedCount)> CloseExpiredPeriodsAsync(long? userId, CancellationToken ct = default);
    Task<IReadOnlyList<BudgetActiveUserDbResult>> GetActiveUserIdsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BudgetRowDbResult>> GetActiveBudgetsForUserAsync(long userId, CancellationToken ct = default);
}
