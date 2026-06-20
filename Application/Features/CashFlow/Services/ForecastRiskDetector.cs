using Application.Features.CashFlow.DbModels;
using Shared.Enums.CashFlow;

namespace Application.Features.CashFlow.Services;

/// <summary>
/// Pure static rules engine. Operates on a completed ForecastComputationResult
/// and appends detected risks into result.Risks.
/// </summary>
internal static class ForecastRiskDetector
{
    public static void Detect(
        ForecastComputationResult result,
        IReadOnlyList<RecurringDefinitionInput> recurringDefs,
        DateOnly today)
    {
        DetectNegativeBalance(result.Risks, result.MonthlyPoints);
        DetectCashShortage(result.Risks, result.MonthlyPoints, result.CurrentBalanceEst);
        DetectGoalsAtRisk(result.Risks, result.GoalProjections);
        DetectSubscriptionRenewals(result.Risks, recurringDefs, today);
        DetectHighExpenseVariance(result.Risks, result.MonthlyPoints);
    }

    // ── Rule 1: Negative balance ──────────────────────────────────────────────

    private static void DetectNegativeBalance(
        List<ForecastRiskData> risks, List<ForecastMonthlyPointData> points)
    {
        foreach (var p in points)
        {
            if (p.RunningBalance < 0)
            {
                risks.Add(new ForecastRiskData
                {
                    RiskType          = (byte)ForecastRiskType.NegativeBalance,
                    Severity          = (byte)ForecastRiskSeverity.High,
                    TitleEn           = "Negative Balance Risk",
                    TitleAr           = "خطر الرصيد السالب",
                    DescriptionEn     = $"Your projected balance may become negative in {p.MonthYear:MMMM yyyy}.",
                    DescriptionAr     = $"قد يصبح رصيدك المتوقع سالبًا في {FormatMonthAr(p.MonthYear)}.",
                    AffectedMonthYear = p.MonthYear,
                    DataPointJson     = $"{{\"month\":\"{p.MonthYear:yyyy-MM}\",\"balance\":{p.RunningBalance}}}"
                });
                return; // first occurrence only
            }
        }
    }

    // ── Rule 2: Cash shortage (< 20 % of starting balance) ───────────────────

    private static void DetectCashShortage(
        List<ForecastRiskData> risks, List<ForecastMonthlyPointData> points, decimal currentBalance)
    {
        if (currentBalance <= 0) return;
        decimal threshold = currentBalance * 0.20m;

        foreach (var p in points)
        {
            if (p.RunningBalance >= 0 && p.RunningBalance < threshold)
            {
                risks.Add(new ForecastRiskData
                {
                    RiskType          = (byte)ForecastRiskType.CashShortage,
                    Severity          = (byte)ForecastRiskSeverity.Medium,
                    TitleEn           = "Low Cash Warning",
                    TitleAr           = "تحذير: انخفاض السيولة",
                    DescriptionEn     = $"Your balance is projected to drop below 20% of current in {p.MonthYear:MMMM yyyy}.",
                    DescriptionAr     = $"من المتوقع أن ينخفض رصيدك إلى أقل من 20% من الرصيد الحالي في {FormatMonthAr(p.MonthYear)}.",
                    AffectedMonthYear = p.MonthYear,
                    DataPointJson     = $"{{\"month\":\"{p.MonthYear:yyyy-MM}\",\"balance\":{p.RunningBalance},\"threshold\":{threshold}}}"
                });
                return; // first occurrence only
            }
        }
    }

    // ── Rule 3: Goals at risk ─────────────────────────────────────────────────

    private static void DetectGoalsAtRisk(
        List<ForecastRiskData> risks, List<ForecastGoalProjectionData> projections)
    {
        foreach (var g in projections)
        {
            if (!g.IsAtRisk) continue;

            risks.Add(new ForecastRiskData
            {
                RiskType          = (byte)ForecastRiskType.GoalAtRisk,
                Severity          = (byte)ForecastRiskSeverity.Medium,
                TitleEn           = $"Goal at Risk: {g.GoalName}",
                TitleAr           = $"الهدف في خطر: {g.GoalName}",
                DescriptionEn     = $"Goal \"{g.GoalName}\" requires {g.RequiredMonthlyContr:N2}/month but current pace is {g.AvgMonthlyPace:N2}/month.",
                DescriptionAr     = $"الهدف \"{g.GoalName}\" يتطلب {g.RequiredMonthlyContr:N2} شهريًا بينما المعدل الحالي {g.AvgMonthlyPace:N2} شهريًا.",
                AffectedMonthYear = g.TargetDate,
                DataPointJson     = $"{{\"goalId\":{g.GoalId},\"required\":{g.RequiredMonthlyContr},\"pace\":{g.AvgMonthlyPace}}}"
            });
        }
    }

    // ── Rule 4: Subscription renewals in next 30 days ─────────────────────────

    private static void DetectSubscriptionRenewals(
        List<ForecastRiskData> risks,
        IReadOnlyList<RecurringDefinitionInput> defs,
        DateOnly today)
    {
        var window = today.AddDays(30);

        foreach (var def in defs)
        {
            if (!def.IsSubscription) continue;
            if (!def.RenewalDate.HasValue) continue;
            if (def.AutoRenew) continue;

            var renewal = def.RenewalDate.Value;
            if (renewal < today || renewal > window) continue;

            string provider = def.ProviderName ?? "Subscription";
            risks.Add(new ForecastRiskData
            {
                RiskType          = (byte)ForecastRiskType.SubscriptionRenewal,
                Severity          = (byte)ForecastRiskSeverity.Low,
                TitleEn           = $"Subscription Renewal: {provider}",
                TitleAr           = $"تجديد اشتراك: {provider}",
                DescriptionEn     = $"{provider} renews on {renewal:MMM d, yyyy} for {def.Amount:N2}.",
                DescriptionAr     = $"يتجدد اشتراك {provider} بتاريخ {renewal:yyyy-MM-dd} بمبلغ {def.Amount:N2}.",
                AffectedMonthYear = new DateOnly(renewal.Year, renewal.Month, 1),
                DataPointJson     = $"{{\"provider\":\"{provider}\",\"renewalDate\":\"{renewal:yyyy-MM-dd}\",\"amount\":{def.Amount}}}"
            });
        }
    }

    // ── Rule 5: High expense variance across projected months ─────────────────

    private static void DetectHighExpenseVariance(
        List<ForecastRiskData> risks, List<ForecastMonthlyPointData> points)
    {
        if (points.Count < 3) return;

        double mean = points.Average(p => (double)p.ProjectedExpense);
        if (mean == 0) return;

        double variance = points.Average(p =>
        {
            double diff = (double)p.ProjectedExpense - mean;
            return diff * diff;
        });
        double cv = Math.Sqrt(variance) / mean;

        if (cv > 0.30)
        {
            risks.Add(new ForecastRiskData
            {
                RiskType      = (byte)ForecastRiskType.HighExpenseVariance,
                Severity      = (byte)ForecastRiskSeverity.Low,
                TitleEn       = "High Expense Variability",
                TitleAr       = "تقلب عالٍ في النفقات",
                DescriptionEn = $"Projected expenses show high month-to-month variability ({cv:P0}).",
                DescriptionAr = $"تُظهر النفقات المتوقعة تقلبًا شهريًا عاليًا ({cv:P0}).",
                DataPointJson = $"{{\"cv\":{cv:F4},\"mean\":{mean:F2}}}"
            });
        }
    }

    // ── Arabic month helper ───────────────────────────────────────────────────

    private static string FormatMonthAr(DateOnly d)
    {
        string[] months = ["يناير","فبراير","مارس","أبريل","مايو","يونيو",
                           "يوليو","أغسطس","سبتمبر","أكتوبر","نوفمبر","ديسمبر"];
        return $"{months[d.Month - 1]} {d.Year}";
    }
}
