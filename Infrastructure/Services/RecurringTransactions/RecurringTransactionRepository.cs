using Application.Features.RecurringTransactions.DbModels;
using Application.Interfaces.Database;
using Application.Interfaces.Repositories;
using Dapper;
using System.Data;

namespace Infrastructure.Services.RecurringTransactions;

internal sealed class RecurringTransactionRepository(IDbExecutor db) : IRecurringTransactionRepository
{
    public async Task<long> CreateAsync(CreateRecurringTransactionDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",             model.UserId,             DbType.Int64);
        p.Add("@CategoryId",         model.CategoryId,         DbType.Int32);
        p.Add("@TransactionTypeId",  model.TransactionTypeId,  DbType.Byte);
        p.Add("@Name",               model.Name,               DbType.String);
        p.Add("@Amount",             model.Amount,             DbType.Decimal);
        p.Add("@Description",        model.Description,        DbType.String);
        p.Add("@FrequencyId",        model.FrequencyId,        DbType.Byte);
        p.Add("@FrequencyInterval",  model.FrequencyInterval,  DbType.Int32);
        p.Add("@FrequencyUnit",      model.FrequencyUnit,      DbType.Byte);
        p.Add("@DayOfMonth",         model.DayOfMonth,         DbType.Byte);
        p.Add("@DayOfWeek",          model.DayOfWeek,          DbType.Byte);
        p.Add("@StartDate",          model.StartDate,          DbType.Date);
        p.Add("@EndDate",            model.EndDate,            DbType.Date);
        p.Add("@IsSubscription",     model.IsSubscription,     DbType.Boolean);
        p.Add("@Notes",              model.Notes,              DbType.String);
        p.Add("@NextGenerationDate", model.NextGenerationDate, DbType.Date);
        p.Add("@ProviderName",       model.ProviderName,       DbType.String);
        p.Add("@WebsiteUrl",         model.WebsiteUrl,         DbType.String);
        p.Add("@AutoRenew",          model.AutoRenew,          DbType.Boolean);
        p.Add("@RenewalDate",        model.RenewalDate,        DbType.Date);
        p.Add("@TrialEndDate",       model.TrialEndDate,       DbType.Date);
        p.Add("@NewId",              dbType: DbType.Int64, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_RecurringTransaction_Create", p, ct);

        return p.Get<long>("@NewId");
    }

    public async Task<RecurringTransactionDbResult?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@Id", id, DbType.Int64);

        return await db.QuerySingleAsync<RecurringTransactionDbResult>(
            "MyMoney.usp_RecurringTransaction_GetById", p, ct);
    }

    public async Task<GetRecurringTransactionsDbResult> GetListAsync(GetRecurringTransactionsDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",            model.UserId,            DbType.Int64);
        p.Add("@StatusId",          model.StatusId,          DbType.Byte);
        p.Add("@TransactionTypeId", model.TransactionTypeId, DbType.Byte);
        p.Add("@IsSubscription",    model.IsSubscription,    DbType.Boolean);
        p.Add("@PageNumber",        model.PageNumber,        DbType.Int32);
        p.Add("@PageSize",          model.PageSize,          DbType.Int32);
        p.Add("@TotalCount",        dbType: DbType.Int32, direction: ParameterDirection.Output);

        var items = await db.QueryListAsync<RecurringTransactionDbResult>(
            "MyMoney.usp_RecurringTransaction_GetList", p, ct);

        return new GetRecurringTransactionsDbResult
        {
            Items      = items,
            TotalCount = p.Get<int>("@TotalCount"),
        };
    }

    public async Task UpdateAsync(UpdateRecurringTransactionDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@Id",                 model.Id,                 DbType.Int64);
        p.Add("@UserId",             model.UserId,             DbType.Int64);
        p.Add("@CategoryId",         model.CategoryId,         DbType.Int32);
        p.Add("@Name",               model.Name,               DbType.String);
        p.Add("@Amount",             model.Amount,             DbType.Decimal);
        p.Add("@Description",        model.Description,        DbType.String);
        p.Add("@FrequencyInterval",  model.FrequencyInterval,  DbType.Int32);
        p.Add("@FrequencyUnit",      model.FrequencyUnit,      DbType.Byte);
        p.Add("@DayOfMonth",         model.DayOfMonth,         DbType.Byte);
        p.Add("@DayOfWeek",          model.DayOfWeek,          DbType.Byte);
        p.Add("@EndDate",            model.EndDate,            DbType.Date);
        p.Add("@Notes",              model.Notes,              DbType.String);
        p.Add("@NextGenerationDate", model.NextGenerationDate, DbType.Date);
        p.Add("@ProviderName",       model.ProviderName,       DbType.String);
        p.Add("@WebsiteUrl",         model.WebsiteUrl,         DbType.String);
        p.Add("@AutoRenew",          model.AutoRenew,          DbType.Boolean);
        p.Add("@RenewalDate",        model.RenewalDate,        DbType.Date);
        p.Add("@TrialEndDate",       model.TrialEndDate,       DbType.Date);

        await db.ExecuteAsync("MyMoney.usp_RecurringTransaction_Update", p, ct);
    }

    public async Task DeleteAsync(long id, long userId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@Id",     id,     DbType.Int64);
        p.Add("@UserId", userId, DbType.Int64);

        await db.ExecuteAsync("MyMoney.usp_RecurringTransaction_Delete", p, ct);
    }

    public async Task<bool> PauseAsync(long id, long userId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@Id",      id,     DbType.Int64);
        p.Add("@UserId",  userId, DbType.Int64);
        p.Add("@Success", dbType: DbType.Boolean, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_RecurringTransaction_Pause", p, ct);
        return p.Get<bool>("@Success");
    }

    public async Task<bool> ResumeAsync(long id, long userId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@Id",      id,     DbType.Int64);
        p.Add("@UserId",  userId, DbType.Int64);
        p.Add("@Success", dbType: DbType.Boolean, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_RecurringTransaction_Resume", p, ct);
        return p.Get<bool>("@Success");
    }

    public async Task<IReadOnlyList<RecurringTransactionDueDbResult>> GetDueAsync(DateOnly upToDate, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UpToDate", upToDate, DbType.Date);

        return await db.QueryListAsync<RecurringTransactionDueDbResult>(
            "MyMoney.usp_RecurringTransaction_GetDue", p, ct);
    }

    public async Task<GenerateTransactionDbResult> GenerateNextAsync(GenerateTransactionDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@DefinitionId",       model.DefinitionId,       DbType.Int64);
        p.Add("@ForDate",            model.ForDate,            DbType.Date);
        p.Add("@NextGenerationDate", model.NextGenerationDate, DbType.Date);
        p.Add("@TransactionId",      dbType: DbType.Int64,    direction: ParameterDirection.Output);
        p.Add("@WasAlreadyDone",     dbType: DbType.Boolean,  direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_RecurringTransaction_GenerateNext", p, ct);

        return new GenerateTransactionDbResult
        {
            TransactionId  = p.Get<long>("@TransactionId"),
            WasAlreadyDone = p.Get<bool>("@WasAlreadyDone"),
        };
    }

    public async Task<IReadOnlyList<UpcomingItemDbResult>> GetUpcomingAsync(int daysAhead, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@DaysAhead", daysAhead, DbType.Int32);

        return await db.QueryListAsync<UpcomingItemDbResult>(
            "MyMoney.usp_RecurringTransaction_GetUpcoming", p, ct);
    }

    public async Task<RecurringTransactionDashboardDbResult> GetDashboardSummaryAsync(long userId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId", userId, DbType.Int64);

        return await db.QuerySingleAsync<RecurringTransactionDashboardDbResult>(
            "MyMoney.usp_RecurringTransaction_GetDashboardSummary", p, ct)
            ?? new RecurringTransactionDashboardDbResult();
    }

    public async Task<IReadOnlyList<UpcomingItemDbResult>> GetUpcomingByUserAsync(long userId, int daysAhead, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",    userId,    DbType.Int64);
        p.Add("@DaysAhead", daysAhead, DbType.Int32);

        return await db.QueryListAsync<UpcomingItemDbResult>(
            "MyMoney.usp_RecurringTransaction_GetUpcomingByUser", p, ct);
    }
}
