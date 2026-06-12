using Shared.Enums.Jobs;

namespace Domain.Entities.Jobs;

public class BackgroundJob
{
    public long                  JobId         { get; set; }
    public string                JobType       { get; set; } = null!;
    public string                Payload       { get; set; } = null!;
    public BackgroundJobStatus   StatusId      { get; set; }
    public BackgroundJobPriority Priority      { get; set; }
    public DateTime              ScheduledAt   { get; set; }
    public DateTime?             PickedUpAt    { get; set; }
    public DateTime?             CompletedAt   { get; set; }
    public int                   AttemptCount  { get; set; }
    public int                   MaxAttempts   { get; set; }
    public DateTime?             LastAttemptAt { get; set; }
    public DateTime?             NextRetryAt   { get; set; }
    public string?               ErrorMessage  { get; set; }
    public DateTime              CreatedAt     { get; set; }
    public long?                 CreatedBy     { get; set; }

    public bool CanRetry    => StatusId == BackgroundJobStatus.Failed && AttemptCount < MaxAttempts;
    public bool IsCompleted => StatusId == BackgroundJobStatus.Completed;
    public bool IsPending   => StatusId == BackgroundJobStatus.Pending;
}
