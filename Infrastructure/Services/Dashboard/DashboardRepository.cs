using Application.Features.Dashboard.DbModels;
using Application.Interfaces.Database;
using Application.Interfaces.Repositories;
using Dapper;
using System.Data;

namespace Infrastructure.Services.Dashboard;

internal sealed class DashboardRepository(IDbExecutor db) : IDashboardRepository
{
    public async Task<(DashboardKpiDbResult                    Kpi,
                       IReadOnlyList<MonthlyTrendDbResult>     Trend,
                       IReadOnlyList<CategoryBreakdownDbResult> Breakdown,
                       IReadOnlyList<RecentTransactionDbResult> Recent)>
        GetSummaryAsync(long userId, long? workspaceId, byte period, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",      userId,      DbType.Int64);
        p.Add("@WorkspaceId", workspaceId, DbType.Int64);
        p.Add("@Period",      period,      DbType.Byte);

        return await db.QueryMultipleAsync(
            "MyMoney.usp_Dashboard_GetSummary",
            async multi =>
            {
                var kpi = await multi.ReadFirstOrDefaultAsync<DashboardKpiDbResult>()
                          ?? new DashboardKpiDbResult(0, 0, 0, 0, 0, 0);

                var trend     = (await multi.ReadAsync<MonthlyTrendDbResult>()).AsList();
                var breakdown = (await multi.ReadAsync<CategoryBreakdownDbResult>()).AsList();
                var recent    = (await multi.ReadAsync<RecentTransactionDbResult>()).AsList();

                return (kpi,
                        (IReadOnlyList<MonthlyTrendDbResult>)trend,
                        (IReadOnlyList<CategoryBreakdownDbResult>)breakdown,
                        (IReadOnlyList<RecentTransactionDbResult>)recent);
            },
            p, ct);
    }
}
