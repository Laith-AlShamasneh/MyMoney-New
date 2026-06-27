namespace Application.Features.CashFlow.Jobs;

public record ComputeForecastPayload(long UserId, long? WorkspaceId = null);
