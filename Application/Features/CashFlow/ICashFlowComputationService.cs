namespace Application.Features.CashFlow;

/// <summary>
/// Internal contract called exclusively by background job handlers and the nightly scheduler.
/// </summary>
public interface ICashFlowComputationService
{
    Task ProcessUserForecastAsync(long userId, long? workspaceId, CancellationToken ct = default);
    Task ProcessAllActiveUsersAsync(CancellationToken ct = default);
}
