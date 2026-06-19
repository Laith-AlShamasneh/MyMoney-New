namespace Application.Features.Goals.Jobs;

public sealed record GoalBehindScheduleCheckPayload();

public sealed record GoalAutoContributionSyncPayload(DateOnly ProcessingDate);
