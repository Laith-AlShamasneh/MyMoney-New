namespace Application.Features.Budget;

public interface IBudgetComputationService
{
    Task ComputeUserBudgetSnapshotAsync(long userId, long budgetId, CancellationToken ct = default);
    Task RunDailyMaintenanceAsync(long? userId, CancellationToken ct = default);
}
