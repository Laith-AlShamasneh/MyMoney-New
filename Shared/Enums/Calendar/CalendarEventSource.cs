namespace Shared.Enums.Calendar;

public enum CalendarEventSource : byte
{
    UserDefined          = 1,
    RecurringTransaction = 2,
    Goal                 = 3,
    Budget               = 4,
    Subscription         = 5,
    Forecast             = 6,
    Insight              = 7,
    Report               = 8,
}
