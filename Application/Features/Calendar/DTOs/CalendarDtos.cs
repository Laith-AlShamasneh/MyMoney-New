namespace Application.Features.Calendar.DTOs;

// ── Requests ──────────────────────────────────────────────────────────────────

public sealed record CreateCalendarEventRequest(
    string  Title,
    string? Description,
    string  EventDate,
    string? StartTime,
    string? EndTime,
    bool    AllDay,
    int     EventTypeId,
    int     Priority,
    int?    LinkedEntityTypeId,
    long?   LinkedEntityId,
    string? ColorHex,
    string? Icon,
    int?    NotifyBefore,
    string? MetadataJson);

public sealed record UpdateCalendarEventRequest(
    long    EventId,
    string  Title,
    string? Description,
    string  EventDate,
    string? StartTime,
    string? EndTime,
    bool    AllDay,
    int     EventTypeId,
    int     Priority,
    string? ColorHex,
    string? Icon,
    int?    NotifyBefore);

public sealed record GetCalendarEventRequest(long EventId);

public sealed record DeleteCalendarEventRequest(long EventId);

public sealed record CompleteCalendarEventRequest(long EventId);

public sealed record GetCalendarByDayRequest(string Date);

public sealed record GetCalendarByWeekRequest(string WeekStart);

public sealed record GetCalendarByMonthRequest(
    int  Year,
    int  Month,
    int? EventTypeId = null);

public sealed record GetCalendarAgendaRequest(
    string? StartDate  = null,
    int     DaysAhead  = 30,
    int     PageNumber = 1,
    int     PageSize   = 20);

public sealed record SearchCalendarRequest(
    string? Keyword     = null,
    int?    EventTypeId = null,
    int?    SourceId    = null,
    string? DateFrom    = null,
    string? DateTo      = null,
    int?    StatusId    = null,
    int?    Priority    = null,
    int     PageNumber  = 1,
    int     PageSize    = 20);

public sealed record DismissReminderRequest(long ReminderId);
public sealed record SnoozeReminderRequest(long ReminderId);
public sealed record MarkReminderClickedRequest(long ReminderId);
public sealed record ReminderHistoryRequest(long ReminderId);

// ── Responses ─────────────────────────────────────────────────────────────────

// Smart reminder popup — one active reminder (event snapshot + reminder state).
public sealed record ActiveReminderDto(
    long     ReminderId,
    long     EventId,
    DateTime ReminderAtUtc,
    int      SnoozeCount,
    int      MaxSnoozes,
    string   Title,
    string?  Description,
    string   EventDate,
    string?  StartTime,
    string?  EndTime,
    bool     AllDay,
    byte     EventTypeId,
    byte     Priority,
    string?  ColorHex,
    string?  Icon,
    string?  Location);

public sealed record ActiveRemindersResponse(IReadOnlyList<ActiveReminderDto> Reminders);

public sealed record SnoozeReminderResponse(int SnoozeCount, int MaxSnoozes, DateTime SnoozedUntilUtc);

public sealed record ReminderHistoryItemDto(
    long     HistoryId,
    long     ReminderId,
    string   Action,
    byte?    FromStatusId,
    byte?    ToStatusId,
    string?  DetailJson,
    DateTime CreatedAtUtc);

public sealed record ReminderHistoryResponse(IReadOnlyList<ReminderHistoryItemDto> Items);

public sealed record CreateCalendarEventResponse(long EventId);

public sealed record CalendarReminderResponse(
    long     ReminderId,
    DateTime ReminderAtUtc,
    byte     StatusId);

public sealed record CalendarEventDetailResponse(
    long     EventId,
    string   Title,
    string?  Description,
    string   EventDate,
    string?  StartTime,
    string?  EndTime,
    bool     AllDay,
    byte     EventTypeId,
    byte     Priority,
    byte     StatusId,
    byte?    LinkedEntityTypeId,
    long?    LinkedEntityId,
    string?  ColorHex,
    string?  Icon,
    int?     NotifyBefore,
    string?  MetadataJson,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? CompletedAtUtc,
    CalendarReminderResponse? Reminder);

public sealed record CalendarEventRow(
    long    EventId,
    byte    SourceId,
    byte    EventTypeId,
    string  EventDate,
    string  TitleEn,
    string  TitleAr,
    string? Description,
    byte    Priority,
    byte    StatusId,
    byte?   LinkedEntityTypeId,
    long?   LinkedEntityId,
    string? ColorHex,
    string? Icon,
    bool    AllDay,
    string? StartTime,
    string? EndTime,
    bool    IsCompleted,
    int?    NotifyBefore,
    decimal? Amount);

public sealed record CalendarDayResponse(IReadOnlyList<CalendarEventRow> Events);

public sealed record CalendarWeekResponse(IReadOnlyList<CalendarEventRow> Events);

public sealed record CalendarMonthResponse(IReadOnlyList<CalendarEventRow> Events);

public sealed record CalendarAgendaResponse(
    int                             TotalCount,
    int                             PageNumber,
    int                             PageSize,
    IReadOnlyList<CalendarEventRow> Items);

public sealed record CalendarSearchResponse(
    int                             TotalCount,
    int                             PageNumber,
    int                             PageSize,
    IReadOnlyList<CalendarEventRow> Items);

public sealed record WeekDaySummary(
    string EventDate,
    int    EventCount,
    bool   IsToday);

public sealed record CalendarDashboardResponse(
    IReadOnlyList<CalendarEventRow>  TodayEvents,
    IReadOnlyList<WeekDaySummary>    WeekSummary,
    IReadOnlyList<CalendarEventRow>  UpcomingBills,
    IReadOnlyList<CalendarEventRow>  UpcomingGoals,
    IReadOnlyList<CalendarEventRow>  UpcomingRecurring);
