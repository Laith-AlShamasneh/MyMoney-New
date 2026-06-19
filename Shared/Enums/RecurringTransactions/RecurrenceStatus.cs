namespace Shared.Enums.RecurringTransactions;

public enum RecurrenceStatus : byte
{
    Active    = 1,
    Paused    = 2,
    Cancelled = 3,
    Expired   = 4,
}
