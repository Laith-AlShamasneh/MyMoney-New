namespace Application.Features.Budget.Jobs;

public record ComputeBudgetSnapshotPayload(long UserId, long BudgetId);
