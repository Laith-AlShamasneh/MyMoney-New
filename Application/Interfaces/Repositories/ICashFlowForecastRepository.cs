using Application.Features.CashFlow.DbModels;

namespace Application.Interfaces.Repositories;

public interface ICashFlowForecastRepository
{
    // ── Computation inputs ────────────────────────────────────────────────────
    Task<ForecastComputationInputs> GetComputationInputsAsync(long userId, CancellationToken ct = default);

    // ── Upsert ────────────────────────────────────────────────────────────────
    Task<long> UpsertForecastAsync(long userId, ForecastComputationResult result, byte horizonMonths, CancellationToken ct = default);

    // ── Child table replace ───────────────────────────────────────────────────
    Task ReplaceMonthlyPointsAsync(long forecastId, long userId, IReadOnlyList<ForecastMonthlyPointData> points, CancellationToken ct = default);
    Task ReplaceRisksAsync(long forecastId, long userId, IReadOnlyList<ForecastRiskData> risks, CancellationToken ct = default);
    Task ReplaceGoalProjectionsAsync(long forecastId, long userId, IReadOnlyList<ForecastGoalProjectionData> projections, CancellationToken ct = default);

    // ── Read ──────────────────────────────────────────────────────────────────
    Task<ForecastFullDbResult?>         GetForecastAsync(long userId, byte horizonMonths, CancellationToken ct = default);
    Task<ForecastDashboardDbResult?>    GetDashboardAsync(long userId, CancellationToken ct = default);

    // ── Risk notifications ────────────────────────────────────────────────────
    Task<IReadOnlyList<UnnotifiedRiskDbResult>> GetUnnotifiedRisksAsync(long userId, byte minSeverity, CancellationToken ct = default);
    Task MarkRiskNotifiedAsync(long riskId, CancellationToken ct = default);

    // ── Scheduler ─────────────────────────────────────────────────────────────
    Task<IReadOnlyList<CashFlowActiveUserDbResult>> GetActiveUsersAsync(int activeDays, CancellationToken ct = default);
}
