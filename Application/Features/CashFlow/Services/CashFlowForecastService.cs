using Application.Common.Constants;
using Application.Features.CashFlow.DbModels;
using Application.Features.CashFlow.DTOs;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Shared.Constants;
using Shared.Enums.CashFlow;
using Shared.Enums.System;
using Shared.Results;

namespace Application.Features.CashFlow.Services;

internal sealed class CashFlowForecastService(
    ICashFlowForecastRepository              cashFlowRepo,
    IFinancialIntelligenceRepository         filRepo,
    INotificationPublisher                   notificationPublisher,
    IUserContext                             userContext,
    IMessageProvider                         messageProvider,
    ICacheService                            cacheService,
    ILogger<CashFlowForecastService>         logger)
    : ICashFlowForecastService, ICashFlowComputationService
{
    private const string ForecastCacheKeyPrefix  = "cashflow:forecast:";
    private const string DashboardCacheKeyPrefix = "cashflow:dashboard:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(4);

    private bool IsArabic => userContext.Language == SystemLanguages.Arabic;

    // ═════════════════════════════════════════════════════════════════════════
    // API-facing methods
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<ServiceResult<CashFlowForecastResponse>> GetForecastAsync(
        GetForecastRequest request,
        CancellationToken  ct = default)
    {
        var userId   = userContext.UserId;
        var isArabic = IsArabic;

        var cacheKey = $"{ForecastCacheKeyPrefix}{userId}:{userContext.WorkspaceId ?? 0}:{request.HorizonMonths}";
        var cached   = await cacheService.GetAsync<CashFlowForecastResponse>(cacheKey);
        if (cached is not null)
        {
            var cachedMsg = await messageProvider.GetMessagesAsync(MessageKeys.CashFlow.ForecastLoaded, ct);
            return ServiceResultFactory.Success(cached, InternalResponseCodes.OK, cachedMsg);
        }

        var db = await cashFlowRepo.GetForecastAsync(userId, userContext.WorkspaceId, (byte)request.HorizonMonths, ct);

        if (db?.Header is null)
        {
            return ServiceResultFactory.Failure<CashFlowForecastResponse>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.CashFlow.ForecastNotAvailable, ct));
        }

        var response = MapForecast(db, isArabic);

        await cacheService.SetAsync(cacheKey, response, CacheTtl);

        var msg = await messageProvider.GetMessagesAsync(MessageKeys.CashFlow.ForecastLoaded, ct);
        return ServiceResultFactory.Success(response, InternalResponseCodes.OK, msg);
    }

    public async Task<ServiceResult<CashFlowDashboardResponse>> GetDashboardAsync(CancellationToken ct = default)
    {
        var userId   = userContext.UserId;
        var isArabic = IsArabic;

        var cacheKey = $"{DashboardCacheKeyPrefix}{userId}:{userContext.WorkspaceId ?? 0}";
        var cached   = await cacheService.GetAsync<CashFlowDashboardResponse>(cacheKey);
        if (cached is not null)
        {
            var cachedMsg = await messageProvider.GetMessagesAsync(MessageKeys.CashFlow.DashboardLoaded, ct);
            return ServiceResultFactory.Success(cached, InternalResponseCodes.OK, cachedMsg);
        }

        var db = await cashFlowRepo.GetDashboardAsync(userId, userContext.WorkspaceId, ct);

        if (db?.Summary is null)
        {
            return ServiceResultFactory.Failure<CashFlowDashboardResponse>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.CashFlow.ForecastNotAvailable, ct));
        }

        var response = MapDashboard(db, isArabic);

        await cacheService.SetAsync(cacheKey, response, CacheTtl);

        var msg = await messageProvider.GetMessagesAsync(MessageKeys.CashFlow.DashboardLoaded, ct);
        return ServiceResultFactory.Success(response, InternalResponseCodes.OK, msg);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Background processing methods (called by ComputeForecastHandler + scheduler)
    // ═════════════════════════════════════════════════════════════════════════

    public async Task ProcessUserForecastAsync(long userId, long? workspaceId, CancellationToken ct = default)
    {
        var inputs = await cashFlowRepo.GetComputationInputsAsync(userId, workspaceId, ct);

        if (inputs.MonthlySnapshots.Count == 0)
        {
            logger.LogInformation(
                "CashFlow: skipping user {UserId} / workspace {WorkspaceId} — no historical snapshots",
                userId, workspaceId);
            return;
        }

        const byte horizonMonths = 12;

        // ── Compute ──────────────────────────────────────────────────────────
        var result = ForecastEngine.Compute(inputs, horizonMonths);
        ForecastRiskDetector.Detect(result, inputs.ActiveRecurringDefs, DateOnly.FromDateTime(DateTime.UtcNow));

        // ── Persist ──────────────────────────────────────────────────────────
        var forecastId = await cashFlowRepo.UpsertForecastAsync(userId, workspaceId, result, horizonMonths, ct);

        await Task.WhenAll(
            cashFlowRepo.ReplaceMonthlyPointsAsync(forecastId, userId, result.MonthlyPoints, ct),
            cashFlowRepo.ReplaceRisksAsync(forecastId, userId, result.Risks, ct),
            cashFlowRepo.ReplaceGoalProjectionsAsync(forecastId, userId, result.GoalProjections, ct));

        // ── Invalidate cache ─────────────────────────────────────────────────
        await Task.WhenAll(
            cacheService.RemoveAsync($"{ForecastCacheKeyPrefix}{userId}:{workspaceId ?? 0}:{horizonMonths}"),
            cacheService.RemoveAsync($"{DashboardCacheKeyPrefix}{userId}:{workspaceId ?? 0}"));

        // ── Notify unnotified risks (severity ≥ Medium) ──────────────────────
        var unnotified = await cashFlowRepo.GetUnnotifiedRisksAsync(userId, workspaceId, minSeverity: 2, ct);
        foreach (var risk in unnotified)
        {
            var code = (ForecastRiskType)risk.RiskType switch
            {
                ForecastRiskType.NegativeBalance => NotificationCodes.CashFlowNegativeBalance,
                ForecastRiskType.CashShortage    => NotificationCodes.CashFlowCashShortage,
                ForecastRiskType.GoalAtRisk       => NotificationCodes.CashFlowGoalAtRisk,
                _                                => null
            };

            if (code is null) continue;

            await notificationPublisher.PublishAsync(
                templateCode: code,
                userId:       userId,
                parameters:   risk.AffectedMonthYear.HasValue
                    ? new Dictionary<string, string> { ["month"] = risk.AffectedMonthYear.Value.ToString("MMMM yyyy") }
                    : null,
                ct: ct);

            await cashFlowRepo.MarkRiskNotifiedAsync(risk.RiskId, ct);
        }

        logger.LogInformation(
            "CashFlow: forecast computed for user {UserId} — {Points} months, {Risks} risks, confidence {Score:F1}",
            userId, result.MonthlyPoints.Count, result.Risks.Count, result.OverallConfidence);
    }

    public async Task ProcessAllActiveUsersAsync(CancellationToken ct = default)
    {
        // usp_CashFlow_GetActiveUsers returns users only; forecasts are per-workspace,
        // so we drive the batch off the active-WORKSPACE list (shared with FIL).
        var workspaces = await filRepo.GetActiveUsersAsync(activeDays: 60, ct);

        foreach (var ws in workspaces)
        {
            try
            {
                await ProcessUserForecastAsync(ws.OwnerUserId, ws.WorkspaceId, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "CashFlow: forecast computation failed for workspace {WorkspaceId}", ws.WorkspaceId);
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Mapping
    // ═════════════════════════════════════════════════════════════════════════

    private static CashFlowForecastResponse MapForecast(ForecastFullDbResult db, bool isArabic)
    {
        var h = db.Header!;

        var points = db.MonthlyPoints.Select(p => new MonthlyPointDto(
            MonthYear:        p.MonthYear.ToString("yyyy-MM"),
            ProjectedIncome:  p.ProjectedIncome,
            ProjectedExpense: p.ProjectedExpense,
            ProjectedNet:     p.ProjectedNet,
            RunningBalance:   p.RunningBalance,
            RecurringIncome:  p.RecurringIncome,
            RecurringExpense: p.RecurringExpense,
            VariableIncome:   p.VariableIncome,
            VariableExpense:  p.VariableExpense,
            ConfidenceScore:  p.ConfidenceScore)).ToList();

        var risks = db.Risks.Select(r => MapRiskDto(r, isArabic)).ToList();

        var goals = db.GoalProjections.Select(g => new GoalProjectionDto(
            GoalId:                     g.GoalId,
            GoalName:                   g.GoalName,
            TargetAmount:               g.TargetAmount,
            CurrentAmount:              g.CurrentAmount,
            TargetDate:                 g.TargetDate?.ToString("yyyy-MM-dd"),
            RequiredMonthlyContribution: g.RequiredMonthlyContr,
            AvgMonthlyPace:             g.AvgMonthlyPace,
            EstimatedCompletionDate:    g.EstimatedComplDate?.ToString("yyyy-MM-dd"),
            IsAtRisk:                   g.IsAtRisk,
            DaysToCompletion:           g.DaysToCompletion)).ToList();

        return new CashFlowForecastResponse(
            ForecastId:               h.ForecastId,
            GeneratedAt:              h.GeneratedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            HorizonMonths:            h.HorizonMonths,
            MonthsOfHistoryUsed:      h.MonthsOfHistoryUsed,
            CurrentBalanceEst:        h.CurrentBalanceEst,
            OverallConfidence:        h.OverallConfidence,
            ConfidenceBand:           h.ConfidenceBand,
            ConfidenceBandLabel:      GetBandLabel(h.ConfidenceBand, isArabic),
            RecurringIncomeMonthly:   h.RecurringIncomeMthly,
            RecurringExpenseMonthly:  h.RecurringExpMthly,
            AvgVariableIncomeMonthly: h.AvgVarIncomeMthly,
            AvgVariableExpenseMonthly: h.AvgVarExpMthly,
            ForecastedEndBalance:     h.ForecastedEndBalance,
            MonthlyTimeline:          points,
            Risks:                    risks,
            GoalProjections:          goals);
    }

    private static CashFlowDashboardResponse MapDashboard(ForecastDashboardDbResult db, bool isArabic)
    {
        var s = db.Summary!;

        var points = db.Points.Select(p => new DashboardMonthlyPointDto(
            MonthYear:        p.MonthYear.ToString("yyyy-MM"),
            ProjectedIncome:  p.ProjectedIncome,
            ProjectedExpense: p.ProjectedExpense,
            ProjectedNet:     p.ProjectedNet,
            RunningBalance:   p.RunningBalance,
            ConfidenceScore:  p.ConfidenceScore)).ToList();

        var risks = db.Risks.Select(r => new ForecastRiskDto(
            RiskId:         r.RiskId,
            RiskType:       r.RiskType,
            Severity:       r.Severity,
            Title:          isArabic ? r.TitleAr : r.TitleEn,
            Description:    isArabic ? r.DescriptionAr : r.DescriptionEn,
            AffectedMonth:  r.AffectedMonthYear?.ToString("yyyy-MM"))).ToList();

        return new CashFlowDashboardResponse(
            ForecastId:              s.ForecastId,
            GeneratedAt:             s.GeneratedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            OverallConfidence:       s.OverallConfidence,
            ConfidenceBand:          s.ConfidenceBand,
            ConfidenceBandLabel:     GetBandLabel(s.ConfidenceBand, isArabic),
            CurrentBalanceEst:       s.CurrentBalanceEst,
            ForecastedEndBalance:    s.ForecastedEndBalance,
            MonthsOfHistoryUsed:     s.MonthsOfHistoryUsed,
            RecurringIncomeMonthly:  s.RecurringIncomeMthly,
            RecurringExpenseMonthly: s.RecurringExpMthly,
            NextMonths:              points,
            TopRisks:                risks);
    }

    private static ForecastRiskDto MapRiskDto(ForecastRiskDbResult r, bool isArabic) =>
        new(
            RiskId:        r.RiskId,
            RiskType:      r.RiskType,
            Severity:      r.Severity,
            Title:         isArabic ? r.TitleAr : r.TitleEn,
            Description:   isArabic ? r.DescriptionAr : r.DescriptionEn,
            AffectedMonth: r.AffectedMonthYear?.ToString("yyyy-MM"));

    private static string GetBandLabel(byte band, bool isArabic) =>
        ((ForecastConfidenceBand)band) switch
        {
            ForecastConfidenceBand.High   => isArabic ? "عالي"   : "High",
            ForecastConfidenceBand.Medium => isArabic ? "متوسط" : "Medium",
            _                             => isArabic ? "منخفض" : "Low"
        };
}
