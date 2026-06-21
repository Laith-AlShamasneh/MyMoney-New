using Application.Features.Calendar.DbModels;

namespace Application.Interfaces.Repositories;

public interface ICalendarRepository
{
    // User-defined events CRUD
    Task<long>                   CreateEventAsync(CreateCalendarEventDbModel model, CancellationToken ct = default);
    Task<int>                    UpdateEventAsync(UpdateCalendarEventDbModel model, CancellationToken ct = default);
    Task<int>                    DeleteEventAsync(long eventId, long userId, CancellationToken ct = default);
    Task<CalendarEventDbResult?> GetEventByIdAsync(long eventId, long userId, CancellationToken ct = default);
    Task<int>                    CompleteEventAsync(long eventId, long userId, CancellationToken ct = default);

    // Aggregated views
    Task<IReadOnlyList<CalendarEventRowDbResult>> GetByDayAsync(long userId, DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<CalendarEventRowDbResult>> GetByWeekAsync(long userId, DateOnly weekStart, DateOnly weekEnd, CancellationToken ct = default);
    Task<IReadOnlyList<CalendarEventRowDbResult>> GetByMonthAsync(long userId, short year, byte month, byte? eventTypeId = null, CancellationToken ct = default);
    Task<GetAgendaDbResult>                       GetAgendaAsync(GetAgendaDbModel model, CancellationToken ct = default);
    Task<CalendarDashboardDbResult>               GetDashboardAsync(long userId, CancellationToken ct = default);
    Task<SearchCalendarDbResult>                  SearchAsync(SearchCalendarDbModel model, CancellationToken ct = default);

    // Reminders
    Task<IReadOnlyList<PendingReminderDbResult>> GetPendingRemindersAsync(int windowMinutes = 5, CancellationToken ct = default);
    Task                                         MarkReminderSentAsync(long reminderId, long? jobId = null, CancellationToken ct = default);
    Task<int>                                    DismissReminderAsync(long reminderId, long userId, CancellationToken ct = default);
}
