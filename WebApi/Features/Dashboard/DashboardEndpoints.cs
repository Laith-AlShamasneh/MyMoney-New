using Application.Interfaces.Services;
using WebApi.Common.Extensions;

namespace WebApi.Features.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/dashboard")
                       .WithTags("Dashboard")
                       .RequireAuthorization();

        group.MapPost("/summary", GetSummaryAsync);
    }

    private static async Task<IResult> GetSummaryAsync(
        IDashboardService dashboardService,
        CancellationToken ct)
    {
        var result = await dashboardService.GetSummaryAsync(ct);
        return result.ToHttpResponse();
    }
}
