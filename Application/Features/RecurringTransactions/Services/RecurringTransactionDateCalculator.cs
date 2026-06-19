using Shared.Enums.RecurringTransactions;

namespace Application.Features.RecurringTransactions.Services;

/// <summary>
/// Pure static date-arithmetic helpers for recurrence calculation.
/// All business rules for when transactions should be generated live here.
/// No DI dependencies — fully unit-testable.
/// </summary>
public static class RecurringTransactionDateCalculator
{
    private const int MaxCatchUpDates = 365;

    /// <summary>
    /// Computes the first generation date on or after <paramref name="startDate"/>
    /// that matches the recurrence rule.
    /// </summary>
    public static DateOnly ComputeFirstGenerationDate(
        DateOnly           startDate,
        RecurrenceFrequency frequency,
        byte?              dayOfMonth,
        byte?              dayOfWeek)
    {
        return frequency switch
        {
            RecurrenceFrequency.Daily     => startDate,
            RecurrenceFrequency.Weekly    => NextWeekday(startDate, (DayOfWeek)(dayOfWeek ?? 1)),
            RecurrenceFrequency.Monthly   => ApplyDayOfMonth(startDate.Year, startDate.Month, dayOfMonth ?? 1, startDate),
            RecurrenceFrequency.Quarterly => ApplyDayOfMonth(startDate.Year, startDate.Month, dayOfMonth ?? 1, startDate),
            RecurrenceFrequency.Yearly    => ApplyDayOfMonth(startDate.Year, startDate.Month, dayOfMonth ?? 1, startDate),
            RecurrenceFrequency.Custom    => startDate,
            _                             => startDate,
        };
    }

    /// <summary>
    /// Computes the next generation date after <paramref name="lastGenerated"/>.
    /// </summary>
    public static DateOnly ComputeNextGenerationDate(
        DateOnly            lastGenerated,
        RecurrenceFrequency frequency,
        int?                frequencyInterval,
        FrequencyUnit?      frequencyUnit,
        byte?               dayOfMonth,
        byte?               dayOfWeek)
    {
        return frequency switch
        {
            RecurrenceFrequency.Daily     => lastGenerated.AddDays(1),
            RecurrenceFrequency.Weekly    => lastGenerated.AddDays(7),
            RecurrenceFrequency.Monthly   => NextMonthDate(lastGenerated, 1, dayOfMonth),
            RecurrenceFrequency.Quarterly => NextMonthDate(lastGenerated, 3, dayOfMonth),
            RecurrenceFrequency.Yearly    => lastGenerated.AddYears(1),
            RecurrenceFrequency.Custom    => AddCustomInterval(lastGenerated, frequencyInterval ?? 1, frequencyUnit ?? FrequencyUnit.Months),
            _                             => lastGenerated.AddMonths(1),
        };
    }

    /// <summary>
    /// Returns all dates between the last generated date (exclusive) and
    /// <paramref name="upToDate"/> (inclusive) that should have been generated.
    /// Capped at <see cref="MaxCatchUpDates"/> to prevent runaway catch-up.
    /// </summary>
    public static IReadOnlyList<DateOnly> ComputeMissedDates(
        DateOnly?           lastGeneratedDate,
        DateOnly            startDate,
        DateOnly            upToDate,
        RecurrenceFrequency frequency,
        int?                frequencyInterval,
        FrequencyUnit?      frequencyUnit,
        byte?               dayOfMonth,
        byte?               dayOfWeek,
        DateOnly?           endDate)
    {
        var effectiveUpTo = endDate.HasValue && endDate.Value < upToDate ? endDate.Value : upToDate;

        // Start cursor: first date that could be due
        var cursor = lastGeneratedDate.HasValue
            ? ComputeNextGenerationDate(lastGeneratedDate.Value, frequency, frequencyInterval, frequencyUnit, dayOfMonth, dayOfWeek)
            : ComputeFirstGenerationDate(startDate, frequency, dayOfMonth, dayOfWeek);

        if (cursor > effectiveUpTo)
            return [];

        var dates = new List<DateOnly>();

        while (cursor <= effectiveUpTo && dates.Count < MaxCatchUpDates)
        {
            dates.Add(cursor);
            cursor = ComputeNextGenerationDate(cursor, frequency, frequencyInterval, frequencyUnit, dayOfMonth, dayOfWeek);
        }

        return dates;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static DateOnly NextMonthDate(DateOnly from, int monthsToAdd, byte? dayOfMonth)
    {
        var target = from.AddMonths(monthsToAdd);
        return ApplyDayOfMonth(target.Year, target.Month, dayOfMonth ?? 1, null);
    }

    /// <summary>
    /// Applies a day-of-month to a given year+month.
    /// dayOfMonth=0 → last day of month.
    /// dayOfMonth 1–28 → literal day (clamped to actual days in month).
    /// <paramref name="notBefore"/> ensures result >= startDate on first generation.
    /// </summary>
    private static DateOnly ApplyDayOfMonth(int year, int month, byte dayOfMonth, DateOnly? notBefore)
    {
        int daysInMonth = DateTime.DaysInMonth(year, month);

        int day = dayOfMonth == 0
            ? daysInMonth
            : Math.Min((int)dayOfMonth, daysInMonth);

        var result = new DateOnly(year, month, day);

        if (notBefore.HasValue && result < notBefore.Value)
        {
            // The target day in this month has already passed — advance to next month
            var next = new DateOnly(year, month, 1).AddMonths(1);
            daysInMonth = DateTime.DaysInMonth(next.Year, next.Month);
            day = dayOfMonth == 0 ? daysInMonth : Math.Min((int)dayOfMonth, daysInMonth);
            result = new DateOnly(next.Year, next.Month, day);
        }

        return result;
    }

    private static DateOnly NextWeekday(DateOnly from, DayOfWeek target)
    {
        // Find the next occurrence of the target weekday on or after `from`
        int daysUntil = ((int)target - (int)from.DayOfWeek + 7) % 7;
        return from.AddDays(daysUntil == 0 ? 0 : daysUntil);
    }

    private static DateOnly AddCustomInterval(DateOnly from, int interval, FrequencyUnit unit)
    {
        return unit switch
        {
            FrequencyUnit.Days   => from.AddDays(interval),
            FrequencyUnit.Weeks  => from.AddDays(interval * 7),
            FrequencyUnit.Months => from.AddMonths(interval),
            FrequencyUnit.Years  => from.AddYears(interval),
            _                    => from.AddMonths(interval),
        };
    }
}
