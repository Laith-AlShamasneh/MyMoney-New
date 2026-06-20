using Application.Features.CashFlow.DbModels;
using Shared.Enums.CashFlow;
using Shared.Enums.Finance;
using Shared.Enums.RecurringTransactions;

namespace Application.Features.CashFlow.Services;

/// <summary>
/// Pure static engine. No DI — deterministic given the same inputs.
/// Mirrors the RecurringTransactionDateCalculator pattern.
/// </summary>
internal static class ForecastEngine
{
    public static ForecastComputationResult Compute(ForecastComputationInputs inputs, byte horizonMonths)
    {
        var today      = DateOnly.FromDateTime(DateTime.UtcNow);
        var startMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(1);

        int historyCount = inputs.MonthlySnapshots.Count;
        if (historyCount == 0)
            return EmptyResult(horizonMonths, inputs.CurrentBalanceEst);

        // ── Historical averages ───────────────────────────────────────────────
        decimal avgHistIncome  = (decimal)inputs.MonthlySnapshots.Average(s => (double)s.TotalIncome);
        decimal avgHistExpense = (decimal)inputs.MonthlySnapshots.Average(s => (double)s.TotalExpense);

        // Representative avg recurring (3-month sample from start of horizon)
        decimal avgRecIncome  = SampleAvgRecurring(inputs.ActiveRecurringDefs, startMonth, TransactionTypes.Income);
        decimal avgRecExpense = SampleAvgRecurring(inputs.ActiveRecurringDefs, startMonth, TransactionTypes.Expense);

        // Variable = historical minus recurring (floored at 0)
        decimal baseVarIncome  = Math.Max(0, avgHistIncome  - avgRecIncome);
        decimal baseVarExpense = Math.Max(0, avgHistExpense - avgRecExpense);

        decimal trendFactor = ComputeTrendFactor(inputs.CategoryTrends);

        // ── Monthly projection loop ───────────────────────────────────────────
        decimal totalProjI = 0, totalProjE = 0, totalRecI = 0, totalRecE = 0;
        decimal runningBalance = inputs.CurrentBalanceEst;

        var points = new List<ForecastMonthlyPointData>(horizonMonths);

        for (int m = 0; m < horizonMonths; m++)
        {
            var target = startMonth.AddMonths(m);

            decimal recInc = SumRecurringForMonth(inputs.ActiveRecurringDefs, target.Year, target.Month, TransactionTypes.Income);
            decimal recExp = SumRecurringForMonth(inputs.ActiveRecurringDefs, target.Year, target.Month, TransactionTypes.Expense);
            decimal varInc = baseVarIncome;
            decimal varExp = baseVarExpense * trendFactor;

            decimal projIncome  = recInc + varInc;
            decimal projExpense = recExp + varExp;
            decimal projNet     = projIncome - projExpense;
            runningBalance     += projNet;

            totalProjI += projIncome;
            totalProjE += projExpense;
            totalRecI  += recInc;
            totalRecE  += recExp;

            points.Add(new ForecastMonthlyPointData
            {
                MonthYear        = target,
                ProjectedIncome  = Round(projIncome),
                ProjectedExpense = Round(projExpense),
                ProjectedNet     = Round(projNet),
                RunningBalance   = Round(runningBalance),
                RecurringIncome  = Round(recInc),
                RecurringExpense = Round(recExp),
                VariableIncome   = Round(varInc),
                VariableExpense  = Round(varExp),
                ConfidenceScore  = 0 // filled below
            });
        }

        // ── Confidence ────────────────────────────────────────────────────────
        decimal baseConfidence = ComputeBaseConfidence(
            inputs.MonthlySnapshots, historyCount,
            totalProjI, totalProjE, totalRecI, totalRecE);

        for (int m = 0; m < points.Count; m++)
        {
            decimal decayed = baseConfidence * (decimal)Math.Pow(0.96, m);
            points[m].ConfidenceScore = Round(Math.Max(0, Math.Min(100, decayed)));
        }

        // ── Goal projections ──────────────────────────────────────────────────
        var goalProjections = ComputeGoalProjections(inputs.ActiveGoals, today);

        byte bandByte = baseConfidence < 35 ? (byte)ForecastConfidenceBand.Low
                      : baseConfidence < 65 ? (byte)ForecastConfidenceBand.Medium
                      :                       (byte)ForecastConfidenceBand.High;

        return new ForecastComputationResult
        {
            MonthsOfHistoryUsed    = historyCount,
            CurrentBalanceEst      = inputs.CurrentBalanceEst,
            OverallConfidence      = Round(baseConfidence),
            ConfidenceBand         = bandByte,
            RecurringIncomeMonthly = horizonMonths > 0 ? Round(totalRecI / horizonMonths) : 0,
            RecurringExpMonthly    = horizonMonths > 0 ? Round(totalRecE / horizonMonths) : 0,
            AvgVarIncomeMonthly    = Round(baseVarIncome),
            AvgVarExpMonthly       = Round(baseVarExpense),
            ForecastedEndBalance   = points.Count > 0 ? points[^1].RunningBalance : inputs.CurrentBalanceEst,
            MonthlyPoints          = points,
            GoalProjections        = goalProjections,
            Risks                  = [] // populated by ForecastRiskDetector
        };
    }

    // ── Confidence helpers ────────────────────────────────────────────────────

    private static decimal ComputeBaseConfidence(
        IReadOnlyList<MonthlySnapshotInput> snapshots, int historyCount,
        decimal totalProjI, decimal totalProjE, decimal totalRecI, decimal totalRecE)
    {
        decimal totalProjected = totalProjI + totalProjE;
        decimal totalRecurring = totalRecI + totalRecE;
        decimal recurringCoverage = totalProjected > 0
            ? Math.Min(100, totalRecurring / totalProjected * 100)
            : 0;

        decimal historyScore = Math.Min(100, historyCount / 6m * 100);
        decimal stabilityScore = ComputeStabilityScore(snapshots);

        return recurringCoverage * 0.50m + historyScore * 0.30m + stabilityScore * 0.20m;
    }

    private static decimal ComputeStabilityScore(IReadOnlyList<MonthlySnapshotInput> snapshots)
    {
        if (snapshots.Count < 2) return 50m;

        double mean = snapshots.Average(s => (double)s.NetBalance);
        if (mean == 0) return 0m;

        double variance = snapshots.Average(s =>
        {
            double diff = (double)s.NetBalance - mean;
            return diff * diff;
        });
        double stdDev = Math.Sqrt(variance);
        double cv = stdDev / Math.Abs(mean);

        return Math.Max(0, Math.Min(100, (decimal)((1.0 - cv) * 100)));
    }

    // ── Recurring projection helpers ──────────────────────────────────────────

    private static decimal SampleAvgRecurring(
        IReadOnlyList<RecurringDefinitionInput> defs, DateOnly startMonth, TransactionTypes type)
    {
        const int sample = 3;
        decimal total = 0;
        for (int m = 0; m < sample; m++)
        {
            var t = startMonth.AddMonths(m);
            total += SumRecurringForMonth(defs, t.Year, t.Month, type);
        }
        return total / sample;
    }

    private static decimal SumRecurringForMonth(
        IReadOnlyList<RecurringDefinitionInput> defs, int year, int month, TransactionTypes type)
    {
        decimal total = 0;
        foreach (var def in defs)
        {
            if ((TransactionTypes)def.TransactionTypeId != type) continue;
            int n = ProjectRecurringForMonth(def, year, month);
            total += def.Amount * n;
        }
        return total;
    }

    /// <summary>
    /// Returns the number of times a recurring definition fires within the given calendar month.
    /// Designed for forward projection only — do not use for catch-up generation.
    /// </summary>
    private static int ProjectRecurringForMonth(RecurringDefinitionInput def, int year, int month)
    {
        var firstDay = new DateOnly(year, month, 1);
        var lastDay  = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

        if (def.StartDate > lastDay) return 0;
        if (def.EndDate.HasValue && def.EndDate.Value < firstDay) return 0;

        return (RecurrenceFrequency)def.FrequencyId switch
        {
            RecurrenceFrequency.Daily     => CountDailyOccurrences(def, firstDay, lastDay),
            RecurrenceFrequency.Weekly    => CountWeeklyOccurrences(def, year, month),
            RecurrenceFrequency.Monthly   => 1,
            RecurrenceFrequency.Quarterly => IsQuarterlyAligned(def.StartDate, year, month) ? 1 : 0,
            RecurrenceFrequency.Yearly    => def.StartDate.Month == month ? 1 : 0,
            RecurrenceFrequency.Custom    => CountCustomOccurrences(def, firstDay, lastDay),
            _                             => 0
        };
    }

    private static int CountDailyOccurrences(RecurringDefinitionInput def, DateOnly firstDay, DateOnly lastDay)
    {
        var effectiveStart = def.StartDate > firstDay ? def.StartDate : firstDay;
        var effectiveEnd   = def.EndDate.HasValue && def.EndDate.Value < lastDay ? def.EndDate.Value : lastDay;
        return effectiveStart > effectiveEnd ? 0 : effectiveEnd.DayNumber - effectiveStart.DayNumber + 1;
    }

    private static int CountWeeklyOccurrences(RecurringDefinitionInput def, int year, int month)
    {
        if (!def.DayOfWeek.HasValue) return 4;

        var targetDow = (DayOfWeek)def.DayOfWeek.Value;
        int daysInMonth = DateTime.DaysInMonth(year, month);
        int count = 0;
        for (int d = 1; d <= daysInMonth; d++)
        {
            if (new DateOnly(year, month, d).DayOfWeek == targetDow)
                count++;
        }
        return count;
    }

    private static bool IsQuarterlyAligned(DateOnly startDate, int year, int month)
    {
        int monthsSinceStart = (year * 12 + month) - (startDate.Year * 12 + startDate.Month);
        return monthsSinceStart >= 0 && monthsSinceStart % 3 == 0;
    }

    private static int CountCustomOccurrences(RecurringDefinitionInput def, DateOnly firstDay, DateOnly lastDay)
    {
        if (!def.FrequencyInterval.HasValue || !def.FrequencyUnit.HasValue) return 0;

        int interval = def.FrequencyInterval.Value;
        var unit     = (FrequencyUnit)def.FrequencyUnit.Value;
        var cursor   = def.StartDate;
        int count    = 0;
        int guard    = 500;

        // Advance cursor to first date on or after firstDay
        while (cursor < firstDay && --guard > 0)
            cursor = AdvanceCustomCursor(cursor, interval, unit);

        // Count how many fall within [firstDay, lastDay]
        while (cursor <= lastDay && guard-- > 0)
        {
            if (cursor >= firstDay) count++;
            cursor = AdvanceCustomCursor(cursor, interval, unit);
        }

        return count;
    }

    private static DateOnly AdvanceCustomCursor(DateOnly cursor, int interval, FrequencyUnit unit) => unit switch
    {
        FrequencyUnit.Days   => cursor.AddDays(interval),
        FrequencyUnit.Weeks  => cursor.AddDays(interval * 7),
        FrequencyUnit.Months => cursor.AddMonths(interval),
        FrequencyUnit.Years  => cursor.AddYears(interval),
        _                    => cursor.AddDays(interval)
    };

    // ── Goal projections ──────────────────────────────────────────────────────

    private static List<ForecastGoalProjectionData> ComputeGoalProjections(
        IReadOnlyList<ActiveGoalInput> goals, DateOnly today)
    {
        var result = new List<ForecastGoalProjectionData>(goals.Count);
        foreach (var goal in goals)
        {
            decimal remaining = goal.TargetAmount - goal.CurrentAmount;

            decimal requiredMonthly = 0;
            if (goal.TargetDate.HasValue)
            {
                int monthsLeft = (goal.TargetDate.Value.Year * 12 + goal.TargetDate.Value.Month)
                               - (today.Year * 12 + today.Month);
                requiredMonthly = monthsLeft > 0 ? remaining / monthsLeft : remaining;
            }

            DateOnly? estimatedCompl = null;
            int?      daysToCompl    = null;

            if (remaining <= 0)
            {
                estimatedCompl = today;
                daysToCompl    = 0;
            }
            else if (goal.AvgMonthlyPace > 0)
            {
                int wholeMonths = (int)Math.Ceiling((double)(remaining / goal.AvgMonthlyPace));
                estimatedCompl  = today.AddMonths(wholeMonths);
                daysToCompl     = estimatedCompl.Value.DayNumber - today.DayNumber;
            }

            bool isAtRisk = goal.TargetDate.HasValue
                         && requiredMonthly > 0
                         && goal.AvgMonthlyPace < requiredMonthly * 0.85m;

            result.Add(new ForecastGoalProjectionData
            {
                GoalId               = goal.GoalId,
                GoalName             = goal.GoalName,
                TargetAmount         = goal.TargetAmount,
                CurrentAmount        = goal.CurrentAmount,
                TargetDate           = goal.TargetDate,
                RequiredMonthlyContr = Round(requiredMonthly),
                AvgMonthlyPace       = goal.AvgMonthlyPace,
                EstimatedComplDate   = estimatedCompl,
                IsAtRisk             = isAtRisk,
                DaysToCompletion     = daysToCompl
            });
        }
        return result;
    }

    // ── Trend factor ──────────────────────────────────────────────────────────

    private static decimal ComputeTrendFactor(IReadOnlyList<CategoryTrendInput> trends)
    {
        decimal factor = 1.0m;
        foreach (var t in trends)
        {
            if (t.TrendDirection == 2) factor += 0.03m;      // Increasing
            else if (t.TrendDirection == 3) factor -= 0.02m; // Decreasing
        }
        return Math.Max(0.85m, Math.Min(1.15m, factor));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ForecastComputationResult EmptyResult(byte horizonMonths, decimal currentBalance) =>
        new()
        {
            MonthsOfHistoryUsed    = 0,
            CurrentBalanceEst      = currentBalance,
            OverallConfidence      = 0,
            ConfidenceBand         = (byte)ForecastConfidenceBand.Low,
            RecurringIncomeMonthly = 0,
            RecurringExpMonthly    = 0,
            AvgVarIncomeMonthly    = 0,
            AvgVarExpMonthly       = 0,
            ForecastedEndBalance   = currentBalance
        };

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
