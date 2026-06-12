namespace Infrastructure.Jobs.Options;

public sealed class BackgroundJobOptions
{
    public int PollingIntervalSeconds { get; init; } = 10;
    public int BatchSize              { get; init; } = 20;
}
