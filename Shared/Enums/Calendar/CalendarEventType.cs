namespace Shared.Enums.Calendar;

public enum CalendarEventType : byte
{
    Reminder    = 1,
    Income      = 2,
    Expense     = 3,
    Goal        = 4,
    Budget      = 5,
    Subscription = 6,
    BillPayment = 7,
    Custom      = 8,
}
