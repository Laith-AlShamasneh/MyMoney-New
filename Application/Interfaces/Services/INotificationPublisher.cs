namespace Application.Interfaces.Services;

/// <summary>
/// Thin publishing façade. Any feature calls this to fire a notification —
/// implementation details (job enqueueing, template rendering, persistence)
/// are entirely hidden from the caller.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>
    /// Publishes a notification asynchronously via the background job system.
    /// </summary>
    /// <param name="templateCode">A <see cref="Application.Common.Constants.NotificationCodes"/> constant.</param>
    /// <param name="userId">Target user.</param>
    /// <param name="parameters">Token-replacement values for the template, e.g. { "ChangedAt", "2025-01-01" }.</param>
    /// <param name="payload">Optional deep-link metadata serialised to JSON, e.g. new { transactionId = 123 }.</param>
    Task PublishAsync(
        string                      templateCode,
        long                        userId,
        Dictionary<string, string>? parameters = null,
        object?                     payload    = null,
        CancellationToken           ct         = default);
}
