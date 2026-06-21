using Application.Features.Calendar.DbModels;
using Application.Interfaces.Database;
using Application.Interfaces.Repositories;
using Dapper;
using System.Data;

namespace Infrastructure.Services.Calendar;

internal sealed class CalendarRepository(IDbExecutor db) : ICalendarRepository
{
    // ── Create ─────────────────────────────────────────────────────────────────

    public async Task<long> CreateEventAsync(CreateCalendarEventDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",             model.UserId,             DbType.Int64);
        p.Add("@Title",              model.Title,              DbType.String);
        p.Add("@Description",        model.Description,        DbType.String);
        p.Add("@EventDate",          model.EventDate,          DbType.Date);
        p.Add("@StartTime",          model.StartTime,          DbType.Time);
        p.Add("@EndTime",            model.EndTime,            DbType.Time);
        p.Add("@AllDay",             model.AllDay,             DbType.Boolean);
        p.Add("@EventTypeId",        model.EventTypeId,        DbType.Byte);
        p.Add("@Priority",           model.Priority,           DbType.Byte);
        p.Add("@LinkedEntityTypeId", model.LinkedEntityTypeId, DbType.Byte);
        p.Add("@LinkedEntityId",     model.LinkedEntityId,     DbType.Int64);
        p.Add("@ColorHex",           model.ColorHex,           DbType.String);
        p.Add("@Icon",               model.Icon,               DbType.String);
        p.Add("@NotifyBefore",       model.NotifyBefore,       DbType.Int32);
        p.Add("@MetadataJson",       model.MetadataJson,       DbType.String);
        p.Add("@NewEventId", dbType: DbType.Int64, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Calendar_Event_Create", p, ct);
        return p.Get<long>("@NewEventId");
    }

    // ── Update ─────────────────────────────────────────────────────────────────

    public async Task<int> UpdateEventAsync(UpdateCalendarEventDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@EventId",      model.EventId,      DbType.Int64);
        p.Add("@UserId",       model.UserId,       DbType.Int64);
        p.Add("@Title",        model.Title,        DbType.String);
        p.Add("@Description",  model.Description,  DbType.String);
        p.Add("@EventDate",    model.EventDate,    DbType.Date);
        p.Add("@StartTime",    model.StartTime,    DbType.Time);
        p.Add("@EndTime",      model.EndTime,      DbType.Time);
        p.Add("@AllDay",       model.AllDay,       DbType.Boolean);
        p.Add("@EventTypeId",  model.EventTypeId,  DbType.Byte);
        p.Add("@Priority",     model.Priority,     DbType.Byte);
        p.Add("@ColorHex",     model.ColorHex,     DbType.String);
        p.Add("@Icon",         model.Icon,         DbType.String);
        p.Add("@NotifyBefore", model.NotifyBefore, DbType.Int32);
        p.Add("@AffectedRows", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Calendar_Event_Update", p, ct);
        return p.Get<int>("@AffectedRows");
    }

    // ── Delete ─────────────────────────────────────────────────────────────────

    public async Task<int> DeleteEventAsync(long eventId, long userId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@EventId", eventId, DbType.Int64);
        p.Add("@UserId",  userId,  DbType.Int64);
        p.Add("@AffectedRows", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Calendar_Event_Delete", p, ct);
        return p.Get<int>("@AffectedRows");
    }

    // ── Get By Id ──────────────────────────────────────────────────────────────

    public async Task<CalendarEventDbResult?> GetEventByIdAsync(long eventId, long userId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@EventId", eventId, DbType.Int64);
        p.Add("@UserId",  userId,  DbType.Int64);

        return await db.QuerySingleAsync<CalendarEventDbResult>("MyMoney.usp_Calendar_Event_Get", p, ct);
    }

    // ── Complete ───────────────────────────────────────────────────────────────

    public async Task<int> CompleteEventAsync(long eventId, long userId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@EventId", eventId, DbType.Int64);
        p.Add("@UserId",  userId,  DbType.Int64);
        p.Add("@AffectedRows", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Calendar_Event_Complete", p, ct);
        return p.Get<int>("@AffectedRows");
    }

    // ── Views ──────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<CalendarEventRowDbResult>> GetByDayAsync(long userId, DateOnly date, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId", userId, DbType.Int64);
        p.Add("@Date",   date,   DbType.Date);

        return await db.QueryListAsync<CalendarEventRowDbResult>("MyMoney.usp_Calendar_GetByDay", p, ct);
    }

    public async Task<IReadOnlyList<CalendarEventRowDbResult>> GetByWeekAsync(long userId, DateOnly weekStart, DateOnly weekEnd, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",    userId,    DbType.Int64);
        p.Add("@WeekStart", weekStart, DbType.Date);
        p.Add("@WeekEnd",   weekEnd,   DbType.Date);

        return await db.QueryListAsync<CalendarEventRowDbResult>("MyMoney.usp_Calendar_GetByWeek", p, ct);
    }

    public async Task<IReadOnlyList<CalendarEventRowDbResult>> GetByMonthAsync(long userId, short year, byte month, byte? eventTypeId = null, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",      userId,      DbType.Int64);
        p.Add("@Year",        year,        DbType.Int16);
        p.Add("@Month",       month,       DbType.Byte);
        p.Add("@EventTypeId", eventTypeId, DbType.Byte);

        return await db.QueryListAsync<CalendarEventRowDbResult>("MyMoney.usp_Calendar_GetByMonth", p, ct);
    }

    public async Task<GetAgendaDbResult> GetAgendaAsync(GetAgendaDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",     model.UserId,     DbType.Int64);
        p.Add("@StartDate",  model.StartDate,  DbType.Date);
        p.Add("@DaysAhead",  model.DaysAhead,  DbType.Int32);
        p.Add("@PageNumber", model.PageNumber, DbType.Int32);
        p.Add("@PageSize",   model.PageSize,   DbType.Int32);

        return await db.QueryMultipleAsync(
            "MyMoney.usp_Calendar_GetAgenda",
            async multi =>
            {
                var countRow = await multi.ReadFirstAsync<TotalCountRow>();
                var items    = (await multi.ReadAsync<CalendarEventRowDbResult>()).ToList();
                return new GetAgendaDbResult
                {
                    TotalCount = countRow.TotalCount,
                    Items      = items,
                };
            },
            p, ct);
    }

    public async Task<CalendarDashboardDbResult> GetDashboardAsync(long userId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId", userId, DbType.Int64);

        return await db.QueryMultipleAsync(
            "MyMoney.usp_Calendar_GetDashboard",
            async multi =>
            {
                var todayEvents       = (await multi.ReadAsync<CalendarEventRowDbResult>()).ToList();
                var weekSummary       = (await multi.ReadAsync<WeekDaySummaryDbResult>()).ToList();
                var upcomingBills     = (await multi.ReadAsync<CalendarEventRowDbResult>()).ToList();
                var upcomingGoals     = (await multi.ReadAsync<CalendarEventRowDbResult>()).ToList();
                var upcomingRecurring = (await multi.ReadAsync<CalendarEventRowDbResult>()).ToList();
                return new CalendarDashboardDbResult
                {
                    TodayEvents       = todayEvents,
                    WeekSummary       = weekSummary,
                    UpcomingBills     = upcomingBills,
                    UpcomingGoals     = upcomingGoals,
                    UpcomingRecurring = upcomingRecurring,
                };
            },
            p, ct);
    }

    public async Task<SearchCalendarDbResult> SearchAsync(SearchCalendarDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",      model.UserId,      DbType.Int64);
        p.Add("@Keyword",     model.Keyword,     DbType.String);
        p.Add("@EventTypeId", model.EventTypeId, DbType.Byte);
        p.Add("@SourceId",    model.SourceId,    DbType.Byte);
        p.Add("@DateFrom",    model.DateFrom,    DbType.Date);
        p.Add("@DateTo",      model.DateTo,      DbType.Date);
        p.Add("@StatusId",    model.StatusId,    DbType.Byte);
        p.Add("@Priority",    model.Priority,    DbType.Byte);
        p.Add("@PageNumber",  model.PageNumber,  DbType.Int32);
        p.Add("@PageSize",    model.PageSize,    DbType.Int32);

        return await db.QueryMultipleAsync(
            "MyMoney.usp_Calendar_Search",
            async multi =>
            {
                var countRow = await multi.ReadFirstAsync<TotalCountRow>();
                var items    = (await multi.ReadAsync<CalendarEventRowDbResult>()).ToList();
                return new SearchCalendarDbResult
                {
                    TotalCount = countRow.TotalCount,
                    Items      = items,
                };
            },
            p, ct);
    }

    // ── Reminders ──────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<PendingReminderDbResult>> GetPendingRemindersAsync(int windowMinutes = 5, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@WindowMinutes", windowMinutes, DbType.Int32);

        return await db.QueryListAsync<PendingReminderDbResult>("MyMoney.usp_Calendar_Reminder_GetPending", p, ct);
    }

    public async Task MarkReminderSentAsync(long reminderId, long? jobId = null, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@ReminderId", reminderId, DbType.Int64);
        p.Add("@JobId",      jobId,      DbType.Int64);

        await db.ExecuteAsync("MyMoney.usp_Calendar_Reminder_MarkSent", p, ct);
    }

    public async Task<int> DismissReminderAsync(long reminderId, long userId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@ReminderId", reminderId, DbType.Int64);
        p.Add("@UserId",     userId,     DbType.Int64);
        p.Add("@AffectedRows", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Calendar_Reminder_Dismiss", p, ct);
        return p.Get<int>("@AffectedRows");
    }
}
