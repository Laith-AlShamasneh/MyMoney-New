using Application.Features.Reports.DbModels;
using Application.Interfaces.Database;
using Application.Interfaces.Repositories;
using Dapper;
using System.Data;

namespace Infrastructure.Services.Reports;

internal sealed class ReportRepository(IDbExecutor db) : IReportRepository
{
    public async Task<IReadOnlyList<ReportTypeDbModel>> GetTypesAsync(CancellationToken ct = default) =>
        await db.QueryListAsync<ReportTypeDbModel>("MyMoney.usp_Report_GetTypes", null, ct);

    public async Task<long> CreateAsync(
        long      userId,
        byte      reportTypeId,
        string    reportTypeKey,
        string    language,
        DateOnly  dateFrom,
        DateOnly  dateTo,
        DateTime  expiresOnUtc,
        CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",        userId,        DbType.Int64);
        p.Add("@ReportTypeId",  reportTypeId,  DbType.Byte);
        p.Add("@ReportTypeKey", reportTypeKey, DbType.String);
        p.Add("@Language",      language,      DbType.String);
        p.Add("@DateFrom",      dateFrom,      DbType.Date);
        p.Add("@DateTo",        dateTo,        DbType.Date);
        p.Add("@ExpiresOnUtc",  expiresOnUtc,  DbType.DateTime2);
        p.Add("@NewId", dbType: DbType.Int64, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Report_Create", p, ct);
        return p.Get<long>("@NewId");
    }

    public Task UpdateToProcessingAsync(long reportId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@ReportId", reportId, DbType.Int64);
        return db.ExecuteAsync("MyMoney.usp_Report_UpdateToProcessing", p, ct);
    }

    public Task CompleteAsync(long reportId, string filePath, long fileSize, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@ReportId", reportId, DbType.Int64);
        p.Add("@FilePath", filePath, DbType.String);
        p.Add("@FileSize", fileSize, DbType.Int64);
        return db.ExecuteAsync("MyMoney.usp_Report_Complete", p, ct);
    }

    public Task FailAsync(long reportId, string errorMessage, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@ReportId",     reportId,     DbType.Int64);
        p.Add("@ErrorMessage", errorMessage, DbType.String);
        return db.ExecuteAsync("MyMoney.usp_Report_Fail", p, ct);
    }

    public async Task<ReportDbModel?> GetByIdAsync(long reportId, long userId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@ReportId", reportId, DbType.Int64);
        p.Add("@UserId",   userId,   DbType.Int64);
        var list = await db.QueryListAsync<ReportDbModel>("MyMoney.usp_Report_GetById", p, ct);
        return list.Count > 0 ? list[0] : null;
    }

    public Task<IReadOnlyList<ReportDbModel>> GetListAsync(long userId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId", userId, DbType.Int64);
        return db.QueryListAsync<ReportDbModel>("MyMoney.usp_Report_GetList", p, ct);
    }

    public async Task<bool> DeleteAsync(long reportId, long userId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@ReportId",    reportId, DbType.Int64);
        p.Add("@UserId",      userId,   DbType.Int64);
        p.Add("@RowsDeleted", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Report_Delete", p, ct);
        return p.Get<int>("@RowsDeleted") > 0;
    }

    public Task ExpireOldAsync(CancellationToken ct = default) =>
        db.ExecuteAsync("MyMoney.usp_Report_ExpireOld", null, ct);
}
