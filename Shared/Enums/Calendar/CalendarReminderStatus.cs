namespace Shared.Enums.Calendar;

/// <summary>
/// Delivery lifecycle of a calendar reminder. Values are persisted in
/// CalendarReminders.StatusId (see migration 018_SmartReminders.sql).
/// </summary>
public enum CalendarReminderStatus : byte
{
    Pending   = 1,   // created, not yet due
    Delivered = 2,   // due → notification created → popup shows (active)
    Dismissed = 3,   // user closed the popup (stays in Notification Center)
    Snoozed   = 4,   // SnoozedUntilUtc set; re-delivered when due
    Clicked   = 6,   // user opened the event ("Go To Event")
    Expired   = 7,   // auto-expired (event long past)
}
