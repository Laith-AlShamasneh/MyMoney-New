namespace Application.Features.Calendar.DbModels;

// ── Create ────────────────────────────────────────────────────────────────────

public class CreateCalendarEventDbModel
{
    public long    UserId             { get; set; }
    public string  Title              { get; set; } = null!;
    public string? Description        { get; set; }
    public DateOnly EventDate         { get; set; }
    public TimeOnly? StartTime        { get; set; }
    public TimeOnly? EndTime          { get; set; }
    public bool    AllDay             { get; set; } = true;
    public byte    EventTypeId        { get; set; } = 1;
    public byte    Priority           { get; set; } = 2;
    public byte?   LinkedEntityTypeId { get; set; }
    public long?   LinkedEntityId     { get; set; }
    public string? ColorHex           { get; set; }
    public string? Icon               { get; set; }
    public int?    NotifyBefore       { get; set; }
    public string? MetadataJson       { get; set; }
}

// ── Update ────────────────────────────────────────────────────────────────────

public class UpdateCalendarEventDbModel
{
    public long    EventId            { get; set; }
    public long    UserId             { get; set; }
    public string  Title              { get; set; } = null!;
    public string? Description        { get; set; }
    public DateOnly EventDate         { get; set; }
    public TimeOnly? StartTime        { get; set; }
    public TimeOnly? EndTime          { get; set; }
    public bool    AllDay             { get; set; }
    public byte    EventTypeId        { get; set; }
    public byte    Priority           { get; set; }
    public string? ColorHex           { get; set; }
    public string? Icon               { get; set; }
    public int?    NotifyBefore       { get; set; }
}

// ── Get / Detail ──────────────────────────────────────────────────────────────

public class CalendarEventDbResult
{
    public long    EventId            { get; set; }
    public string  Title              { get; set; } = null!;
    public string? Description        { get; set; }
    public DateOnly EventDate         { get; set; }
    public string? StartTime          { get; set; }
    public string? EndTime            { get; set; }
    public bool    AllDay             { get; set; }
    public byte    EventTypeId        { get; set; }
    public byte    Priority           { get; set; }
    public byte    StatusId           { get; set; }
    public byte?   LinkedEntityTypeId { get; set; }
    public long?   LinkedEntityId     { get; set; }
    public string? ColorHex           { get; set; }
    public string? Icon               { get; set; }
    public int?    NotifyBefore       { get; set; }
    public string? MetadataJson       { get; set; }
    public DateTime  CreatedAtUtc     { get; set; }
    public DateTime? UpdatedAtUtc     { get; set; }
    public DateTime? CompletedAtUtc   { get; set; }

    // Reminder (nullable — may not have one)
    public long?     ReminderId        { get; set; }
    public DateTime? ReminderAtUtc     { get; set; }
    public byte?     ReminderStatusId  { get; set; }
}

// ── Unified row (returned by all aggregated view SPs) ─────────────────────────

public class CalendarEventRowDbResult
{
    public long    EventId            { get; set; }
    public byte    SourceId           { get; set; }
    public byte    EventTypeId        { get; set; }
    public DateOnly EventDate         { get; set; }
    public string  TitleEn            { get; set; } = null!;
    public string  TitleAr            { get; set; } = null!;
    public string? Description        { get; set; }
    public byte    Priority           { get; set; }
    public byte    StatusId           { get; set; }
    public byte?   LinkedEntityTypeId { get; set; }
    public long?   LinkedEntityId     { get; set; }
    public string? ColorHex           { get; set; }
    public string? Icon               { get; set; }
    public bool    AllDay             { get; set; }
    public string? StartTime          { get; set; }
    public string? EndTime            { get; set; }
    public bool    IsCompleted        { get; set; }
    public int?    NotifyBefore       { get; set; }
    public decimal? Amount            { get; set; }
}

// ── Search / List ──────────────────────────────────────────────────────────────

public class SearchCalendarDbModel
{
    public long    UserId      { get; set; }
    public string? Keyword     { get; set; }
    public byte?   EventTypeId { get; set; }
    public byte?   SourceId    { get; set; }
    public DateOnly? DateFrom  { get; set; }
    public DateOnly? DateTo    { get; set; }
    public byte?   StatusId    { get; set; }
    public byte?   Priority    { get; set; }
    public int     PageNumber  { get; set; } = 1;
    public int     PageSize    { get; set; } = 20;
}

public class SearchCalendarDbResult
{
    public int                                   TotalCount { get; set; }
    public IReadOnlyList<CalendarEventRowDbResult> Items    { get; set; } = [];
}

// ── Agenda ────────────────────────────────────────────────────────────────────

public class GetAgendaDbModel
{
    public long    UserId     { get; set; }
    public DateOnly? StartDate { get; set; }
    public int     DaysAhead  { get; set; } = 30;
    public int     PageNumber { get; set; } = 1;
    public int     PageSize   { get; set; } = 20;
}

public class GetAgendaDbResult
{
    public int                                   TotalCount { get; set; }
    public IReadOnlyList<CalendarEventRowDbResult> Items    { get; set; } = [];
}

// ── Dashboard ─────────────────────────────────────────────────────────────────

public class CalendarDashboardDbResult
{
    public IReadOnlyList<CalendarEventRowDbResult> TodayEvents          { get; set; } = [];
    public IReadOnlyList<WeekDaySummaryDbResult>   WeekSummary          { get; set; } = [];
    public IReadOnlyList<CalendarEventRowDbResult> UpcomingBills        { get; set; } = [];
    public IReadOnlyList<CalendarEventRowDbResult> UpcomingGoals        { get; set; } = [];
    public IReadOnlyList<CalendarEventRowDbResult> UpcomingRecurring    { get; set; } = [];
}

public class WeekDaySummaryDbResult
{
    public DateOnly EventDate  { get; set; }
    public int      EventCount { get; set; }
    public bool     IsToday    { get; set; }
}

// ── Reminders ─────────────────────────────────────────────────────────────────

public class PendingReminderDbResult
{
    public long    ReminderId   { get; set; }
    public long    EventId      { get; set; }
    public long    UserId       { get; set; }
    public DateTime ReminderAtUtc { get; set; }
    public string  Title        { get; set; } = null!;
    public DateOnly EventDate   { get; set; }
    public byte    EventTypeId  { get; set; }
}

public sealed class TotalCountRow { public int TotalCount { get; set; } }
