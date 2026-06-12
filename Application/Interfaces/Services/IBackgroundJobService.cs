namespace Application.Interfaces.Services;

public interface IBackgroundJobService
{
    Task EnqueueAsync<TPayload>(
        string      jobType,
        TPayload    payload,
        byte        priority    = 2,
        DateTime?   scheduledAt = null,
        int         maxAttempts = 3,
        CancellationToken ct    = default);
}
