using Application.Common.Constants;
using Application.Features.FinancialIntelligence;
using Application.Features.FinancialIntelligence.Jobs;
using Infrastructure.Jobs;

namespace Infrastructure.Jobs.Handlers;

internal sealed class MinuteAnomalyJobHandler(
    IFinancialIntelligenceService filService) : JobHandlerBase<MinuteAnomalyPayload>
{
    public override string JobType => JobTypes.MinuteAnomalyCheck;

    protected override Task HandleAsync(MinuteAnomalyPayload payload, CancellationToken ct) =>
        filService.ProcessMinuteAnomalyAsync(payload.CheckFromUtc, ct);
}
