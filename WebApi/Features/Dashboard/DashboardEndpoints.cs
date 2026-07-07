using Application.Features.Dashboard.DTOs;
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
        DashboardSummaryRequest? request,
        IDashboardService        dashboardService,
        CancellationToken        ct)
    {
        // Body is optional; default to current-month when omitted.
        var result = await dashboardService.GetSummaryAsync(request?.Period ?? 0, ct);
        return result.ToHttpResponse();
    }
}
