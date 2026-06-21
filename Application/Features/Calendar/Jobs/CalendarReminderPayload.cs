namespace Application.Features.Calendar.Jobs;

public sealed record CalendarReminderPayload(
    long   ReminderId,
    long   EventId,
    long   UserId,
    string EventTitle,
    string EventDate,
    byte   EventTypeId);
