namespace Application.Features.FinancialIntelligence;

/// <summary>
/// Internal processing contract called exclusively by background job handlers.
/// Separated from IFinancialIntelligenceService to prevent leaking processing
/// concerns into the public API service contract.
/// </summary>
public interface IFILBackgroundProcessingService
{
    Task ProcessDailyAsync(int year, int month, int day, CancellationToken ct = default);
    Task ProcessMonthlyAsync(int year, int month, CancellationToken ct = default);
    Task ProcessHourlyAnomalyAsync(DateTime fromUtc, CancellationToken ct = default);
}
