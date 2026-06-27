using Application.Features.Dashboard.DTOs;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Shared.Constants;
using Shared.Enums.System;
using Shared.Results;

namespace Application.Features.Dashboard.Services;

internal sealed class DashboardService(
    IDashboardRepository dashboardRepository,
    IUserContext         userContext,
    IMessageProvider     messageProvider) : IDashboardService
{
    public async Task<ServiceResult<DashboardSummaryResponse>> GetSummaryAsync(CancellationToken ct = default)
    {
        var (kpi, trend, breakdown, recent) =
            await dashboardRepository.GetSummaryAsync(userContext.UserId, userContext.WorkspaceId, ct);

        // ── KPI calculations ────────────────────────────────────────────────
        var currentNet  = kpi.CurrentIncome  - kpi.CurrentExpenses;
        var previousNet = kpi.PreviousIncome - kpi.PreviousExpenses;

        decimal? incomeChange   = ComputeChange(kpi.CurrentIncome,   kpi.PreviousIncome);
        decimal? expensesChange = ComputeChange(kpi.CurrentExpenses, kpi.PreviousExpenses);
        decimal? netChange      = previousNet != 0
            ? Math.Round((currentNet - previousNet) / Math.Abs(previousNet) * 100, 1)
            : (decimal?)null;
        int? countChange = kpi.CurrentTransactionCount - kpi.PreviousTransactionCount;

        var kpiSummary = new KpiSummary(
            CurrentIncome:           kpi.CurrentIncome,
            CurrentExpenses:         kpi.CurrentExpenses,
            CurrentNet:              currentNet,
            CurrentTransactionCount: kpi.CurrentTransactionCount,
            IncomeChangePercent:     incomeChange,
            ExpensesChangePercent:   expensesChange,
            NetChangePercent:        netChange,
            TransactionCountChange:  countChange);

        // ── Category breakdown with share % ────────────────────────────────
        var totalExpenses = breakdown.Sum(b => b.TotalAmount);
        var breakdownItems = breakdown
            .Select(b => new CategoryBreakdownItem(
                CategoryId:  b.CategoryId,
                NameEn:      b.NameEn,
                NameAr:      b.NameAr,
                TotalAmount: b.TotalAmount,
                Percentage:  totalExpenses > 0
                    ? Math.Round(b.TotalAmount / totalExpenses * 100, 1)
                    : 0m))
            .ToList();

        // ── Monthly trend ───────────────────────────────────────────────────
        var trendItems = trend
            .Select(t => new MonthlyTrendItem(t.Year, t.Month, t.Income, t.Expenses))
            .ToList();

        // ── Recent transactions ─────────────────────────────────────────────
        var recentItems = recent
            .Select(r => new RecentTransactionItem(
                TransactionId:    r.TransactionId,
                Amount:           r.Amount,
                TransactionTypeId: r.TransactionTypeId,
                Description:      r.Description,
                TransactionDate:  r.TransactionDate,
                CategoryNameEn:   r.CategoryNameEn,
                CategoryNameAr:   r.CategoryNameAr,
                CategoryIcon:     r.CategoryIcon))
            .ToList();

        var response = new DashboardSummaryResponse(
            Kpi:                kpiSummary,
            MonthlyTrend:       trendItems,
            CategoryBreakdown:  breakdownItems,
            RecentTransactions: recentItems);

        var message = await messageProvider.GetMessagesAsync(MessageKeys.Dashboard.LoadedSuccessfully, ct);
        return ServiceResultFactory.Success(response, InternalResponseCodes.OK, message);
    }

    private static decimal? ComputeChange(decimal current, decimal previous)
    {
        if (previous == 0) return null;
        return Math.Round((current - previous) / previous * 100, 1);
    }
}
