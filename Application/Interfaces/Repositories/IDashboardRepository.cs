using Application.Features.Dashboard.DbModels;

namespace Application.Interfaces.Repositories;

public interface IDashboardRepository
{
    Task<(DashboardKpiDbResult                    Kpi,
          IReadOnlyList<MonthlyTrendDbResult>     Trend,
          IReadOnlyList<CategoryBreakdownDbResult> Breakdown,
          IReadOnlyList<RecentTransactionDbResult> Recent)>
        GetSummaryAsync(long userId, long? workspaceId, byte period, CancellationToken ct = default);
}
