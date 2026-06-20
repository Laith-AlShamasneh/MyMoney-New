using Application.Features.CashFlow.DbModels;
using Application.Interfaces.Database;
using Application.Interfaces.Repositories;
using Dapper;
using System.Data;
using System.Text.Json;

namespace Infrastructure.Services.CashFlow;

internal sealed class CashFlowForecastRepository(IDbExecutor db) : ICashFlowForecastRepository
{
    // ── Computation inputs ────────────────────────────────────────────────────

    public Task<ForecastComputationInputs> GetComputationInputsAsync(long userId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId", userId, DbType.Int64);

        return db.QueryMultipleAsync(
            "MyMoney.usp_CashFlow_GetComputationInputs",
            async grid =>
            {
                var snapshots  = (await grid.ReadAsync<MonthlySnapshotInput>()).ToList();
                var recurring  = (await grid.ReadAsync<RecurringDefinitionInput>()).ToList();
                var goals      = (await grid.ReadAsync<ActiveGoalInput>()).ToList();
                var trends     = (await grid.ReadAsync<CategoryTrendInput>()).ToList();
                var balanceRow = await grid.ReadFirstOrDefaultAsync<BalanceRow>();

                return new ForecastComputationInputs
                {
                    MonthlySnapshots     = snapshots,
                    ActiveRecurringDefs  = recurring,
                    ActiveGoals          = goals,
                    CategoryTrends       = trends,
                    CurrentBalanceEst    = balanceRow?.CumulativeNetBalance ?? 0
                };
            },
            p, ct);
    }

    // ── Upsert forecast header ────────────────────────────────────────────────

    public async Task<long> UpsertForecastAsync(
        long                       userId,
        ForecastComputationResult  result,
        byte                       horizonMonths,
        CancellationToken          ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",               userId,                        DbType.Int64);
        p.Add("@HorizonMonths",        horizonMonths,                 DbType.Byte);
        p.Add("@MonthsOfHistoryUsed",  (byte)result.MonthsOfHistoryUsed, DbType.Byte);
        p.Add("@CurrentBalanceEst",    result.CurrentBalanceEst,      DbType.Decimal);
        p.Add("@OverallConfidence",    result.OverallConfidence,      DbType.Decimal);
        p.Add("@ConfidenceBand",       result.ConfidenceBand,         DbType.Byte);
        p.Add("@RecurringIncomeMthly", result.RecurringIncomeMonthly, DbType.Decimal);
        p.Add("@RecurringExpMthly",    result.RecurringExpMonthly,    DbType.Decimal);
        p.Add("@AvgVarIncomeMthly",    result.AvgVarIncomeMonthly,    DbType.Decimal);
        p.Add("@AvgVarExpMthly",       result.AvgVarExpMonthly,       DbType.Decimal);
        p.Add("@ForecastedEndBalance", result.ForecastedEndBalance,   DbType.Decimal);
        p.Add("@ForecastId", dbType: DbType.Int64, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_CashFlow_Forecast_Upsert", p, ct);
        return p.Get<long>("@ForecastId");
    }

    // ── Child table replacements ──────────────────────────────────────────────

    public async Task ReplaceMonthlyPointsAsync(
        long                                forecastId,
        long                                userId,
        IReadOnlyList<ForecastMonthlyPointData> points,
        CancellationToken                   ct = default)
    {
        var json = points.Count == 0 ? "[]" : JsonSerializer.Serialize(
            points.Select(p => new
            {
                MonthYear        = p.MonthYear.ToString("yyyy-MM-01"),
                p.ProjectedIncome,
                p.ProjectedExpense,
                p.ProjectedNet,
                p.RunningBalance,
                p.RecurringIncome,
                p.RecurringExpense,
                p.VariableIncome,
                p.VariableExpense,
                p.ConfidenceScore
            }));

        var dp = new DynamicParameters();
        dp.Add("@ForecastId", forecastId, DbType.Int64);
        dp.Add("@UserId",     userId,     DbType.Int64);
        dp.Add("@PointsJson", json,       DbType.String);
        await db.ExecuteAsync("MyMoney.usp_CashFlow_MonthlyPoints_Replace", dp, ct);
    }

    public async Task ReplaceRisksAsync(
        long                              forecastId,
        long                              userId,
        IReadOnlyList<ForecastRiskData>   risks,
        CancellationToken                 ct = default)
    {
        var json = risks.Count == 0 ? "[]" : JsonSerializer.Serialize(
            risks.Select(r => new
            {
                r.RiskType,
                r.Severity,
                r.TitleEn,
                r.TitleAr,
                r.DescriptionEn,
                r.DescriptionAr,
                AffectedMonthYear = r.AffectedMonthYear?.ToString("yyyy-MM-01"),
                r.DataPointJson
            }));

        var dp = new DynamicParameters();
        dp.Add("@ForecastId", forecastId, DbType.Int64);
        dp.Add("@UserId",     userId,     DbType.Int64);
        dp.Add("@RisksJson",  json,       DbType.String);
        await db.ExecuteAsync("MyMoney.usp_CashFlow_Risks_Replace", dp, ct);
    }

    public async Task ReplaceGoalProjectionsAsync(
        long                                      forecastId,
        long                                      userId,
        IReadOnlyList<ForecastGoalProjectionData> projections,
        CancellationToken                         ct = default)
    {
        var json = projections.Count == 0 ? "[]" : JsonSerializer.Serialize(
            projections.Select(g => new
            {
                g.GoalId,
                g.GoalName,
                g.TargetAmount,
                g.CurrentAmount,
                TargetDate           = g.TargetDate?.ToString("yyyy-MM-dd"),
                g.RequiredMonthlyContr,
                g.AvgMonthlyPace,
                EstimatedComplDate   = g.EstimatedComplDate?.ToString("yyyy-MM-dd"),
                g.IsAtRisk,
                g.DaysToCompletion
            }));

        var dp = new DynamicParameters();
        dp.Add("@ForecastId",      forecastId, DbType.Int64);
        dp.Add("@UserId",          userId,     DbType.Int64);
        dp.Add("@ProjectionsJson", json,       DbType.String);
        await db.ExecuteAsync("MyMoney.usp_CashFlow_GoalProjections_Replace", dp, ct);
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    public Task<ForecastFullDbResult?> GetForecastAsync(
        long userId, byte horizonMonths, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",        userId,        DbType.Int64);
        p.Add("@HorizonMonths", horizonMonths, DbType.Byte);

        return db.QueryMultipleAsync<ForecastFullDbResult?>(
            "MyMoney.usp_CashFlow_GetForecast",
            async grid =>
            {
                var header      = await grid.ReadFirstOrDefaultAsync<ForecastHeaderDbResult>();
                var points      = (await grid.ReadAsync<ForecastPointDbResult>()).ToList();
                var risks       = (await grid.ReadAsync<ForecastRiskDbResult>()).ToList();
                var projections = (await grid.ReadAsync<ForecastGoalProjectionDbResult>()).ToList();

                if (header is null) return null;

                return new ForecastFullDbResult
                {
                    Header          = header,
                    MonthlyPoints   = points,
                    Risks           = risks,
                    GoalProjections = projections
                };
            },
            p, ct);
    }

    public Task<ForecastDashboardDbResult?> GetDashboardAsync(
        long userId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId", userId, DbType.Int64);

        return db.QueryMultipleAsync<ForecastDashboardDbResult?>(
            "MyMoney.usp_CashFlow_GetDashboard",
            async grid =>
            {
                var summary = await grid.ReadFirstOrDefaultAsync<ForecastDashboardSummaryDbResult>();
                var points  = (await grid.ReadAsync<ForecastDashboardPointDbResult>()).ToList();
                var risks   = (await grid.ReadAsync<ForecastDashboardRiskDbResult>()).ToList();

                if (summary is null) return null;

                return new ForecastDashboardDbResult
                {
                    Summary = summary,
                    Points  = points,
                    Risks   = risks
                };
            },
            p, ct);
    }

    // ── Risk notifications ────────────────────────────────────────────────────

    public Task<IReadOnlyList<UnnotifiedRiskDbResult>> GetUnnotifiedRisksAsync(
        long userId, byte minSeverity, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",      userId,      DbType.Int64);
        p.Add("@MinSeverity", minSeverity, DbType.Byte);
        return db.QueryListAsync<UnnotifiedRiskDbResult>(
            "MyMoney.usp_CashFlow_GetUnnotifiedRisks", p, ct);
    }

    public Task MarkRiskNotifiedAsync(long riskId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@RiskId", riskId, DbType.Int64);
        return db.ExecuteAsync("MyMoney.usp_CashFlow_Risk_MarkNotified", p, ct);
    }

    // ── Scheduler ─────────────────────────────────────────────────────────────

    public Task<IReadOnlyList<CashFlowActiveUserDbResult>> GetActiveUsersAsync(
        int activeDays, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@ActiveDays", activeDays, DbType.Int32);
        return db.QueryListAsync<CashFlowActiveUserDbResult>(
            "MyMoney.usp_CashFlow_GetActiveUsers", p, ct);
    }

    // ── Private types ─────────────────────────────────────────────────────────

    private sealed class BalanceRow
    {
        public decimal CumulativeNetBalance { get; set; }
    }
}
