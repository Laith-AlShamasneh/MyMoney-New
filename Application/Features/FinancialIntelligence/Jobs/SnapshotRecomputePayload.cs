namespace Application.Features.FinancialIntelligence.Jobs;

public record SnapshotRecomputePayload(
    long UserId,
    int  Year,
    int  Month);
