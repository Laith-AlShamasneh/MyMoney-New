namespace Application.Features.FinancialIntelligence.Jobs;

public record DailyFILPayload(
    int Year,
    int Month,
    int Day);
