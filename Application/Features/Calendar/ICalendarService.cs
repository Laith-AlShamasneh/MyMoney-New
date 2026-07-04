using Application.Features.Calendar.DTOs;
using Shared.Results;

namespace Application.Features.Calendar;

public interface ICalendarService
{
    Task<ServiceResult<CreateCalendarEventResponse>> CreateEventAsync(CreateCalendarEventRequest request, CancellationToken ct = default);
    Task<ServiceResult<object?>>                     UpdateEventAsync(UpdateCalendarEventRequest request, CancellationToken ct = default);
    Task<ServiceResult<object?>>                     DeleteEventAsync(long eventId, CancellationToken ct = default);
    Task<ServiceResult<CalendarEventDetailResponse>> GetEventAsync(long eventId, CancellationToken ct = default);
    Task<ServiceResult<object?>>                     CompleteEventAsync(long eventId, CancellationToken ct = default);

    Task<ServiceResult<CalendarDayResponse>>      GetByDayAsync(GetCalendarByDayRequest request, CancellationToken ct = default);
    Task<ServiceResult<CalendarWeekResponse>>     GetByWeekAsync(GetCalendarByWeekRequest request, CancellationToken ct = default);
    Task<ServiceResult<CalendarMonthResponse>>    GetByMonthAsync(GetCalendarByMonthRequest request, CancellationToken ct = default);
    Task<ServiceResult<CalendarAgendaResponse>>   GetAgendaAsync(GetCalendarAgendaRequest request, CancellationToken ct = default);
    Task<ServiceResult<CalendarDashboardResponse>> GetDashboardAsync(CancellationToken ct = default);
    Task<ServiceResult<CalendarSearchResponse>>   SearchAsync(SearchCalendarRequest request, CancellationToken ct = default);

    Task<ServiceResult<object?>> DismissReminderAsync(long reminderId, CancellationToken ct = default);

    // Smart reminders
    Task<ServiceResult<ActiveRemindersResponse>> GetActiveRemindersAsync(CancellationToken ct = default);
    Task<ServiceResult<SnoozeReminderResponse>>  SnoozeReminderAsync(long reminderId, CancellationToken ct = default);
    Task<ServiceResult<object?>>                 MarkReminderClickedAsync(long reminderId, CancellationToken ct = default);
    Task<ServiceResult<ReminderHistoryResponse>> GetReminderHistoryAsync(long reminderId, CancellationToken ct = default);
}
