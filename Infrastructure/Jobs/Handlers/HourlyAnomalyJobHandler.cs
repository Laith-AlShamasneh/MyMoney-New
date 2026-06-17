using Application.Common.Constants;
using Application.Features.FinancialIntelligence;
using Application.Features.FinancialIntelligence.Jobs;
using Infrastructure.Jobs;

namespace Infrastructure.Jobs.Handlers;

internal sealed class HourlyAnomalyJobHandler(
    IFinancialIntelligenceService filService) : JobHandlerBase<HourlyAnomalyPayload>
{
    public override string JobType => JobTypes.HourlyAnomalyCheck;

    protected override Task HandleAsync(HourlyAnomalyPayload payload, CancellationToken ct) =>
        filService.ProcessHourlyAnomalyAsync(payload.CheckFromUtc, ct);
}
