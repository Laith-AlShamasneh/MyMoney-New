using System.Text.Json;
using Application.Features.FinancialIntelligence.Rules;
using Application.Common.Constants;
using Shared.Enums.Intelligence;

namespace Application.Features.FinancialIntelligence.Services;

/// <summary>
/// Rule-based financial intelligence engine. Evaluates a user's snapshot + category
/// context and produces insight and recommendation candidates.
///
/// This implementation is deliberately replaceable: swap IFinancialRulesEngine for an
/// AI/ML implementation in Infrastructure without touching any other code.
/// </summary>
internal sealed class FinancialRulesEngine : IFinancialRulesEngine
{
    // Thresholds — centralised so they can be made configurable later.
    private const decimal OverspendingThreshold      = 0.80m;  // 80 % of rolling average
    private const decimal SpendingSpikeThreshold     = 0.30m;  // 30 % increase
    private const decimal PositiveBehaviorThreshold  = 0.20m;  // 20 % decrease
    private const decimal HighExpenseRatioThreshold  = 0.80m;  // 80 % of income
    private const decimal UnusualTransactionMultiple = 2.0m;   // 2× average
    private const int     ConsistentMonthsRequired   = 3;

    // ── Insight evaluation ────────────────────────────────────────────────────

    public IReadOnlyList<InsightCandidate> EvaluateInsights(FinancialRulesContext ctx)
    {
        var candidates = new List<InsightCandidate>();

        EvaluateHighExpenseRatio(ctx, candidates);
        EvaluateSpendingSpike(ctx, candidates);
        EvaluateOverspendingAlert(ctx, candidates);
        EvaluatePositiveBehavior(ctx, candidates);
        EvaluateConsistentSaver(ctx, candidates);

        return candidates.AsReadOnly();
    }

    // ── Recommendation evaluation ─────────────────────────────────────────────

    public IReadOnlyList<RecommendationCandidate> EvaluateRecommendations(FinancialRulesContext ctx)
    {
        var candidates = new List<RecommendationCandidate>();

        RecommendTopCategoryReduction(ctx, candidates);
        RecommendSavingsTarget(ctx, candidates);

        return candidates.AsReadOnly();
    }

    // ── Individual rules ──────────────────────────────────────────────────────

    private static void EvaluateHighExpenseRatio(FinancialRulesContext ctx, List<InsightCandidate> out_)
    {
        var snap = ctx.CurrentSnapshot;
        if (snap is null || snap.TotalIncome <= 0) return;

        var ratio = snap.TotalExpense / snap.TotalIncome;
        if (ratio < HighExpenseRatioThreshold) return;

        var pct = Math.Round(ratio * 100, 1);
        out_.Add(new InsightCandidate
        {
            Type           = (byte)InsightType.Warning,
            Code           = InsightCodes.HighExpenseRatio,
            Severity       = ratio >= 1.0m ? (byte)InsightSeverity.Critical : (byte)InsightSeverity.High,
            TitleEn        = "High Expense Ratio",
            TitleAr        = "نسبة مصروفات مرتفعة",
            DescriptionEn  = $"Your expenses reached {pct}% of your income this month. Consider reviewing your spending.",
            DescriptionAr  = $"بلغت مصروفاتك {pct}% من دخلك هذا الشهر. حاول مراجعة إنفاقك.",
            DataPointJson  = Serialize(new { ratio = pct, income = snap.TotalIncome, expense = snap.TotalExpense }),
            FireNotification = true,
            NotificationCode = NotificationCodes.FILHighExpenseRatio
        });
    }

    private static void EvaluateSpendingSpike(FinancialRulesContext ctx, List<InsightCandidate> out_)
    {
        if (ctx.PreviousCategoryData.Count == 0) return;

        var prevLookup = ctx.PreviousCategoryData.ToDictionary(c => c.CategoryId);

        foreach (var curr in ctx.CurrentCategoryData)
        {
            if (!prevLookup.TryGetValue(curr.CategoryId, out var prev)) continue;
            if (prev.TotalSpent <= 0) continue;

            var change = (curr.TotalSpent - prev.TotalSpent) / prev.TotalSpent;
            if (change < SpendingSpikeThreshold) continue;

            var pct = Math.Round(change * 100, 1);
            out_.Add(new InsightCandidate
            {
                Type              = (byte)InsightType.Warning,
                Code              = InsightCodes.SpendingSpike,
                Severity          = (byte)InsightSeverity.Medium,
                RelatedCategoryId = curr.CategoryId,
                TitleEn           = $"Spending Spike: {curr.NameEn}",
                TitleAr           = $"ارتفاع في إنفاق: {curr.NameAr}",
                DescriptionEn     = $"Your spending on {curr.NameEn} increased by {pct}% compared to last month.",
                DescriptionAr     = $"ارتفع إنفاقك على {curr.NameAr} بنسبة {pct}% مقارنةً بالشهر الماضي.",
                DataPointJson     = Serialize(new { categoryId = curr.CategoryId, change = pct, current = curr.TotalSpent, previous = prev.TotalSpent }),
                FireNotification  = true,
                NotificationCode  = NotificationCodes.FILSpendingSpike,
                NotificationParameters = new Dictionary<string, string>
                {
                    { "CategoryName", curr.NameEn },
                    { "ChangePercent", pct.ToString("F1") }
                }
            });
        }
    }

    private static void EvaluateOverspendingAlert(FinancialRulesContext ctx, List<InsightCandidate> out_)
    {
        if (ctx.RecentSnapshots.Count < 2) return;

        // Rolling 3-month average expense per category
        var rollingLookup = ctx.PreviousCategoryData
            .GroupBy(c => c.CategoryId)
            .ToDictionary(g => g.Key, g => g.Average(x => x.TotalSpent));

        foreach (var curr in ctx.CurrentCategoryData)
        {
            if (!rollingLookup.TryGetValue(curr.CategoryId, out var avg)) continue;
            if (avg <= 0) continue;

            var usage = curr.TotalSpent / avg;
            if (usage < OverspendingThreshold) continue;

            var pct = Math.Round(usage * 100, 1);
            out_.Add(new InsightCandidate
            {
                Type              = (byte)InsightType.Warning,
                Code              = InsightCodes.OverspendingAlert,
                Severity          = usage >= 1.0m ? (byte)InsightSeverity.High : (byte)InsightSeverity.Medium,
                RelatedCategoryId = curr.CategoryId,
                TitleEn           = $"Overspending Alert: {curr.NameEn}",
                TitleAr           = $"تنبيه الإنفاق الزائد: {curr.NameAr}",
                DescriptionEn     = $"Spending on {curr.NameEn} is at {pct}% of your monthly average.",
                DescriptionAr     = $"إنفاقك على {curr.NameAr} وصل إلى {pct}% من متوسطك الشهري.",
                DataPointJson     = Serialize(new { categoryId = curr.CategoryId, usage = pct, current = curr.TotalSpent, average = avg }),
                FireNotification  = usage >= 1.0m,
                NotificationCode  = NotificationCodes.FILOverspendingAlert,
                NotificationParameters = new Dictionary<string, string>
                {
                    { "CategoryName", curr.NameEn },
                    { "UsagePercent", pct.ToString("F1") }
                }
            });
        }
    }

    private static void EvaluatePositiveBehavior(FinancialRulesContext ctx, List<InsightCandidate> out_)
    {
        if (ctx.PreviousCategoryData.Count == 0) return;

        var prevLookup = ctx.PreviousCategoryData.ToDictionary(c => c.CategoryId);

        foreach (var curr in ctx.CurrentCategoryData)
        {
            if (!prevLookup.TryGetValue(curr.CategoryId, out var prev)) continue;
            if (prev.TotalSpent <= 0) continue;

            var change = (prev.TotalSpent - curr.TotalSpent) / prev.TotalSpent;
            if (change < PositiveBehaviorThreshold) continue;

            var pct = Math.Round(change * 100, 1);
            out_.Add(new InsightCandidate
            {
                Type              = (byte)InsightType.Achievement,
                Code              = InsightCodes.PositiveBehavior,
                Severity          = (byte)InsightSeverity.Low,
                RelatedCategoryId = curr.CategoryId,
                TitleEn           = $"Great Job: {curr.NameEn}",
                TitleAr           = $"أحسنت: {curr.NameAr}",
                DescriptionEn     = $"You reduced spending on {curr.NameEn} by {pct}% compared to last month.",
                DescriptionAr     = $"لقد قللت إنفاقك على {curr.NameAr} بنسبة {pct}% مقارنةً بالشهر الماضي.",
                DataPointJson     = Serialize(new { categoryId = curr.CategoryId, reduction = pct }),
                FireNotification  = true,
                NotificationCode  = NotificationCodes.FILAchievement,
                NotificationParameters = new Dictionary<string, string>
                {
                    { "CategoryName", curr.NameEn },
                    { "ReductionPercent", pct.ToString("F1") }
                }
            });
        }
    }

    private static void EvaluateConsistentSaver(FinancialRulesContext ctx, List<InsightCandidate> out_)
    {
        if (ctx.RecentSnapshots.Count < ConsistentMonthsRequired) return;

        var positiveMonths = ctx.RecentSnapshots
            .Take(ConsistentMonthsRequired)
            .Count(s => s.NetBalance > 0);

        if (positiveMonths < ConsistentMonthsRequired) return;

        out_.Add(new InsightCandidate
        {
            Type          = (byte)InsightType.Achievement,
            Code          = InsightCodes.ConsistentSaver,
            Severity      = (byte)InsightSeverity.Low,
            TitleEn       = "Consistent Saver",
            TitleAr       = "مدخر ثابت",
            DescriptionEn = $"You've maintained a positive net balance for {ConsistentMonthsRequired} consecutive months. Excellent financial discipline!",
            DescriptionAr = $"حافظت على رصيد صافي إيجابي لمدة {ConsistentMonthsRequired} أشهر متتالية. انضباط مالي ممتاز!",
            DataPointJson = Serialize(new { months = ConsistentMonthsRequired }),
            FireNotification = true,
            NotificationCode = NotificationCodes.FILAchievement
        });
    }

    // ── Recommendation rules ──────────────────────────────────────────────────

    private static void RecommendTopCategoryReduction(FinancialRulesContext ctx, List<RecommendationCandidate> out_)
    {
        if (ctx.CurrentSnapshot is null || ctx.CurrentSnapshot.TotalExpense <= 0) return;

        var top = ctx.CurrentCategoryData
            .OrderByDescending(c => c.TotalSpent)
            .FirstOrDefault();

        if (top is null || top.PercentOfTotal < 30m) return;

        var potential = Math.Round(top.TotalSpent * 0.10m, 2);

        out_.Add(new RecommendationCandidate
        {
            Type                = (byte)RecommendationType.ReduceSpending,
            Code                = RecommendationCodes.ReviewTopCategory,
            RelatedCategoryId   = top.CategoryId,
            Priority            = 2,
            TitleEn             = $"Review {top.NameEn} Spending",
            TitleAr             = $"راجع إنفاق {top.NameAr}",
            MessageEn           = $"{top.NameEn} accounts for {Math.Round(top.PercentOfTotal, 1)}% of your total expenses. A 10% reduction could save ~{potential:N2} this month.",
            MessageAr           = $"تشكّل {top.NameAr} {Math.Round(top.PercentOfTotal, 1)}% من إجمالي مصروفاتك. تخفيض 10% يمكن أن يوفر لك ~{potential:N2} هذا الشهر.",
            ExpectedImpactValue = potential
        });
    }

    private static void RecommendSavingsTarget(FinancialRulesContext ctx, List<RecommendationCandidate> out_)
    {
        var snap = ctx.CurrentSnapshot;
        if (snap is null || snap.TotalIncome <= 0 || snap.NetBalance >= snap.TotalIncome * 0.20m) return;

        var target = Math.Round(snap.TotalIncome * 0.20m, 2);
        var gap    = Math.Round(target - snap.NetBalance, 2);
        if (gap <= 0) return;

        out_.Add(new RecommendationCandidate
        {
            Type                = (byte)RecommendationType.SaveMore,
            Code                = RecommendationCodes.SavingsTarget20Pct,
            Priority            = 2,
            TitleEn             = "Aim for 20% Savings",
            TitleAr             = "استهدف توفير 20%",
            MessageEn           = $"Financial experts recommend saving at least 20% of income. You need ~{gap:N2} more to reach this target.",
            MessageAr           = $"يُوصي الخبراء الماليون بتوفير 20% على الأقل من الدخل. تحتاج إلى ~{gap:N2} إضافيًا للوصول إلى هذا الهدف.",
            ExpectedImpactValue = gap
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string Serialize(object data) =>
        JsonSerializer.Serialize(data);
}

/// <summary>String constants for insight codes (mirror the Code column in FinancialInsights).</summary>
internal static class InsightCodes
{
    public const string HighExpenseRatio   = "HighExpenseRatio";
    public const string SpendingSpike      = "SpendingSpike";
    public const string OverspendingAlert  = "OverspendingAlert";
    public const string PositiveBehavior   = "PositiveBehavior";
    public const string ConsistentSaver    = "ConsistentSaver";
    public const string UnusualTransaction = "UnusualTransaction";
}

/// <summary>String constants for recommendation codes.</summary>
internal static class RecommendationCodes
{
    public const string ReviewTopCategory  = "ReviewTopCategory";
    public const string SavingsTarget20Pct = "SavingsTarget20Pct";
}
