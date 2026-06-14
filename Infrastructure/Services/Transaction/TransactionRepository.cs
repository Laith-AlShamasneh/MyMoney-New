using Application.Features.Transaction.DbModels;
using Application.Interfaces.Database;
using Application.Interfaces.Repositories;
using Dapper;
using System.Data;

namespace Infrastructure.Services.Transaction;

internal sealed class TransactionRepository(IDbExecutor db) : ITransactionRepository
{
    public async Task<TransactionSearchDbResult> SearchAsync(
        TransactionSearchDbModel model,
        CancellationToken        ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",      model.UserId,      DbType.Int64);
        p.Add("@TypeId",      model.TypeId,      DbType.Byte);
        p.Add("@CategoryId",  model.CategoryId,  DbType.Int32);
        p.Add("@DateFrom",    model.DateFrom,    DbType.Date);
        p.Add("@DateTo",      model.DateTo,      DbType.Date);
        p.Add("@AmountMin",   model.AmountMin,   DbType.Decimal);
        p.Add("@AmountMax",   model.AmountMax,   DbType.Decimal);
        p.Add("@Search",      model.Search,      DbType.String);
        p.Add("@SortBy",      model.SortBy,      DbType.String);
        p.Add("@SortDir",     model.SortDir,     DbType.String);
        p.Add("@PageNumber",  model.PageNumber,  DbType.Int32);
        p.Add("@PageSize",    model.PageSize,    DbType.Int32);
        p.Add("@TotalCount",    dbType: DbType.Int32,    direction: ParameterDirection.Output);
        p.Add("@TotalIncome",   dbType: DbType.Decimal,  direction: ParameterDirection.Output);
        p.Add("@TotalExpenses", dbType: DbType.Decimal,  direction: ParameterDirection.Output);
        p.Add("@AvgAmount",     dbType: DbType.Decimal,  direction: ParameterDirection.Output);
        p.Add("@MaxAmount",     dbType: DbType.Decimal,  direction: ParameterDirection.Output);

        var items = await db.QueryListAsync<TransactionRowDbResult>(
            "MyMoney.usp_Transaction_Search", p, ct);

        return new TransactionSearchDbResult
        {
            Items        = items,
            TotalCount   = p.Get<int>("@TotalCount"),
            TotalIncome  = p.Get<decimal>("@TotalIncome"),
            TotalExpenses= p.Get<decimal>("@TotalExpenses"),
            AvgAmount    = p.Get<decimal>("@AvgAmount"),
            MaxAmount    = p.Get<decimal>("@MaxAmount"),
        };
    }

    public async Task<TransactionAnalyticsDbResult> GetAnalyticsAsync(
        TransactionAnalyticsDbModel model,
        CancellationToken           ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",   model.UserId,   DbType.Int64);
        p.Add("@DateFrom", model.DateFrom, DbType.Date);
        p.Add("@DateTo",   model.DateTo,   DbType.Date);

        return await db.QueryMultipleAsync(
            "MyMoney.usp_Transaction_GetAnalytics",
            async multi =>
            {
                var breakdown = (await multi.ReadAsync<TransactionCategoryBreakdownDbResult>()).AsList();
                var trend     = (await multi.ReadAsync<TransactionTrendPointDbResult>()).AsList();

                return new TransactionAnalyticsDbResult
                {
                    CategoryBreakdown = breakdown,
                    MonthlyTrend      = trend,
                };
            }, p, ct);
    }

    public async Task<TransactionByIdDbResult?> GetByIdAsync(
        long              userId,
        long              transactionId,
        CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",        userId,        DbType.Int64);
        p.Add("@TransactionId", transactionId, DbType.Int64);

        return await db.QuerySingleAsync<TransactionByIdDbResult>(
            "MyMoney.usp_Transaction_GetById", p, ct);
    }

    public async Task<long> CreateAsync(CreateTransactionDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",            model.UserId,            DbType.Int64);
        p.Add("@CategoryId",        model.CategoryId,        DbType.Int32);
        p.Add("@TransactionTypeId", model.TransactionTypeId, DbType.Byte);
        p.Add("@Amount",            model.Amount,            DbType.Decimal);
        p.Add("@Description",       model.Description,       DbType.String);
        p.Add("@TransactionDate",   model.TransactionDate,   DbType.Date);
        p.Add("@Notes",             model.Notes,             DbType.String);
        p.Add("@NewId", dbType: DbType.Int64, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Transaction_Create", p, ct);

        return p.Get<long>("@NewId");
    }

    public async Task<int> UpdateAsync(UpdateTransactionDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",            model.UserId,            DbType.Int64);
        p.Add("@TransactionId",     model.TransactionId,     DbType.Int64);
        p.Add("@CategoryId",        model.CategoryId,        DbType.Int32);
        p.Add("@TransactionTypeId", model.TransactionTypeId, DbType.Byte);
        p.Add("@Amount",            model.Amount,            DbType.Decimal);
        p.Add("@Description",       model.Description,       DbType.String);
        p.Add("@TransactionDate",   model.TransactionDate,   DbType.Date);
        p.Add("@Notes",             model.Notes,             DbType.String);
        p.Add("@AffectedRows", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Transaction_Update", p, ct);

        return p.Get<int>("@AffectedRows");
    }

    public async Task<int> DeleteAsync(DeleteTransactionDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",        model.UserId,        DbType.Int64);
        p.Add("@TransactionId", model.TransactionId, DbType.Int64);
        p.Add("@AffectedRows", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Transaction_Delete", p, ct);

        return p.Get<int>("@AffectedRows");
    }
}
