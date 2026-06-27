using Application.Features.RecurringTransactions.DbModels;

namespace Application.Interfaces.Repositories;

public interface IRecurringTransactionRepository
{
    Task<long>  CreateAsync(CreateRecurringTransactionDbModel model, CancellationToken ct = default);
    Task<RecurringTransactionDbResult?> GetByIdAsync(long id, long userId, long? workspaceId, CancellationToken ct = default);
    Task<GetRecurringTransactionsDbResult> GetListAsync(GetRecurringTransactionsDbModel model, CancellationToken ct = default);
    Task       UpdateAsync(UpdateRecurringTransactionDbModel model, CancellationToken ct = default);
    Task       DeleteAsync(long id, long userId, long? workspaceId, CancellationToken ct = default);
    Task<bool> PauseAsync(long id, long userId, long? workspaceId, CancellationToken ct = default);
    Task<bool> ResumeAsync(long id, long userId, long? workspaceId, CancellationToken ct = default);

    // Scheduler / engine
    Task<IReadOnlyList<RecurringTransactionDueDbResult>> GetDueAsync(DateOnly upToDate, CancellationToken ct = default);
    Task<GenerateTransactionDbResult>                    GenerateNextAsync(GenerateTransactionDbModel model, CancellationToken ct = default);
    Task<IReadOnlyList<UpcomingItemDbResult>>            GetUpcomingAsync(int daysAhead, CancellationToken ct = default);

    // Dashboard
    Task<RecurringTransactionDashboardDbResult> GetDashboardSummaryAsync(long userId, long? workspaceId, CancellationToken ct = default);
    Task<IReadOnlyList<UpcomingItemDbResult>>   GetUpcomingByUserAsync(long userId, int daysAhead, CancellationToken ct = default);
}
