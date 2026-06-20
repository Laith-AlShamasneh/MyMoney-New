using Application.Features.CashFlow;
using Application.Features.CashFlow.DTOs;
using WebApi.Common.Extensions;
using WebApi.Common.Filters;

namespace WebApi.Features.CashFlow;

public static class CashFlowEndpoints
{
    public static void MapCashFlowEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("api/cash-flow")
                       .WithTags("CashFlow")
                       .RequireAuthorization();

        group.MapPost("forecast", async (
            GetForecastRequest         request,
            ICashFlowForecastService   service,
            CancellationToken          ct) =>
        {
            var result = await service.GetForecastAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetForecastRequest>>();

        group.MapPost("dashboard", async (
            ICashFlowForecastService service,
            CancellationToken        ct) =>
        {
            var result = await service.GetDashboardAsync(ct);
            return result.ToHttpResponse();
        });
    }
}
