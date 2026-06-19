namespace Application.Features.RecurringTransactions;

/// <summary>
/// Internal background-processing contract for the recurring transaction engine.
/// Not exposed through the public API — only called by job handlers.
/// </summary>
public interface IRecurringTransactionEngineService
{
    Task ProcessDueTransactionsAsync(DateOnly upToDate, CancellationToken ct = default);
    Task SendUpcomingNotificationsAsync(int daysAhead, CancellationToken ct = default);
}
