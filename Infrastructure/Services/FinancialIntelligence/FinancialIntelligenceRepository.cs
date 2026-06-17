using Application.Features.FinancialIntelligence.DbModels;
using Application.Interfaces.Database;
using Application.Interfaces.Repositories;
using Dapper;
using System.Data;
using System.Text.Json;

namespace Infrastructure.Services.FinancialIntelligence;

internal sealed class FinancialIntelligenceRepository(IDbExecutor db) : IFinancialIntelligenceRepository
{
    // ── Snapshots ─────────────────────────────────────────────────────────────

    public async Task<ComputedSnapshotDbResult?> ComputeMonthlySnapshotAsync(
        ComputeSnapshotDbModel model,
        CancellationToken      ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId", model.UserId, DbType.Int64);
        p.Add("@Year",   model.Year,   DbType.Int32);
        p.Add("@Month",  model.Month,  DbType.Int32);
        return await db.QuerySingleAsync<ComputedSnapshotDbResult?>(
            "MyMoney.usp_FIL_Snapshot_Compute", p, ct);
    }

    public async Task UpsertSnapshotAsync(UpsertSnapshotDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",                  model.UserId,                  DbType.Int64);
        p.Add("@SnapshotDate",            model.SnapshotDate,            DbType.Date);
        p.Add("@PeriodType",              model.PeriodType,              DbType.Byte);
        p.Add("@TotalIncome",             model.TotalIncome,             DbType.Decimal);
        p.Add("@TotalExpense",            model.TotalExpense,            DbType.Decimal);
        p.Add("@NetBalance",              model.NetBalance,              DbType.Decimal);
        p.Add("@AverageDailySpend",       model.AverageDailySpend,       DbType.Decimal);
        p.Add("@AverageTransactionValue", model.AverageTransactionValue, DbType.Decimal);
        p.Add("@TransactionCount",        model.TransactionCount,        DbType.Int32);
        p.Add("@TopCategoryId",           model.TopCategoryId,           DbType.Int32);
        await db.ExecuteAsync("MyMoney.usp_FIL_Snapshot_Upsert", p, ct);
    }

    public async Task<SnapshotDbResult?> GetLatestSnapshotAsync(long userId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId", userId, DbType.Int64);
        return await db.QuerySingleAsync<SnapshotDbResult?>(
            "MyMoney.usp_FIL_Snapshot_GetLatest", p, ct);
    }

    public async Task<IReadOnlyList<SnapshotDbResult>> GetRecentSnapshotsAsync(
        long userId, int months, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId", userId,  DbType.Int64);
        p.Add("@Months", months,  DbType.Int32);
        return await db.QueryListAsync<SnapshotDbResult>(
            "MyMoney.usp_FIL_Snapshot_GetRecent", p, ct);
    }

    // ── Category Analytics ────────────────────────────────────────────────────

    public async Task<IReadOnlyList<CategoryAnalyticsDbResult>> ComputeCategoryAnalyticsAsync(
        ComputeCategoryAnalyticsDbModel model,
        CancellationToken               ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId", model.UserId, DbType.Int64);
        p.Add("@Year",   model.Year,   DbType.Int32);
        p.Add("@Month",  model.Month,  DbType.Int32);
        return await db.QueryListAsync<CategoryAnalyticsDbResult>(
            "MyMoney.usp_FIL_CategoryAnalytics_Compute", p, ct);
    }

    public async Task UpsertCategoryAnalyticsAsync(
        long                                     userId,
        IReadOnlyList<CategoryAnalyticsDbResult> rows,
        CancellationToken                        ct = default)
    {
        if (rows.Count == 0) return;

        var json = JsonSerializer.Serialize(
            rows.Select(r => new
            {
                categoryId          = r.CategoryId,
                periodStart         = r.PeriodStart.ToString("yyyy-MM-dd"),
                periodEnd           = r.PeriodEnd.ToString("yyyy-MM-dd"),
                totalSpent          = r.TotalSpent,
                transactionCount    = r.TransactionCount,
                averageSpent        = r.AverageSpent,
                percentageOfTotal   = r.PercentageOfTotal,
                trendDirection      = r.TrendDirection,
                previousPeriodTotal = r.PreviousPeriodTotal,
                changePercentage    = r.ChangePercentage
            }));

        var p = new DynamicParameters();
        p.Add("@UserId",           userId, DbType.Int64);
        p.Add("@CategoryDataJson", json,   DbType.String);
        await db.ExecuteAsync("MyMoney.usp_FIL_CategoryAnalytics_BulkUpsert", p, ct);
    }

    public async Task<IReadOnlyList<CategoryAnalyticsDbResult>> GetCategoryAnalyticsAsync(
        long userId, int year, int month, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId", userId, DbType.Int64);
        p.Add("@Year",   year,   DbType.Int32);
        p.Add("@Month",  month,  DbType.Int32);
        return await db.QueryListAsync<CategoryAnalyticsDbResult>(
            "MyMoney.usp_FIL_CategoryAnalytics_GetByPeriod", p, ct);
    }

    // ── Insights ──────────────────────────────────────────────────────────────

    public async Task<long> CreateInsightAsync(CreateInsightDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",            model.UserId,            DbType.Int64);
        p.Add("@Type",              model.Type,              DbType.Byte);
        p.Add("@Code",              model.Code,              DbType.String);
        p.Add("@TitleEn",           model.TitleEn,           DbType.String);
        p.Add("@TitleAr",           model.TitleAr,           DbType.String);
        p.Add("@DescriptionEn",     model.DescriptionEn,     DbType.String);
        p.Add("@DescriptionAr",     model.DescriptionAr,     DbType.String);
        p.Add("@Severity",          model.Severity,          DbType.Byte);
        p.Add("@RelatedCategoryId", model.RelatedCategoryId, DbType.Int32);
        p.Add("@DataPointJson",     model.DataPointJson,     DbType.String);
        p.Add("@ExpiresAtUtc",      model.ExpiresAtUtc,      DbType.DateTime2);
        p.Add("@NewId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        await db.ExecuteAsync("MyMoney.usp_FIL_Insight_Create", p, ct);
        return p.Get<long>("@NewId");
    }

    public Task<GetInsightsDbResult> GetInsightsAsync(GetInsightsDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",     model.UserId,     DbType.Int64);
        p.Add("@IsRead",     model.IsRead,     DbType.Boolean);
        p.Add("@PageNumber", model.PageNumber, DbType.Int32);
        p.Add("@PageSize",   model.PageSize,   DbType.Int32);

        return db.QueryMultipleAsync(
            "MyMoney.usp_FIL_Insight_GetList",
            async multi =>
            {
                var items  = (await multi.ReadAsync<InsightRowDbResult>()).AsList();
                var counts = await multi.ReadFirstOrDefaultAsync<InsightCountsRow>()
                             ?? new InsightCountsRow(0, 0);
                return new GetInsightsDbResult
                {
                    Items       = items,
                    TotalCount  = counts.TotalCount,
                    UnreadCount = counts.UnreadCount
                };
            },
            p, ct);
    }

    public async Task<int> MarkInsightReadAsync(long userId, long insightId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",    userId,    DbType.Int64);
        p.Add("@InsightId", insightId, DbType.Int64);
        p.Add("@RowsAffected", dbType: DbType.Int32, direction: ParameterDirection.Output);
        await db.ExecuteAsync("MyMoney.usp_FIL_Insight_MarkRead", p, ct);
        return p.Get<int>("@RowsAffected");
    }

    public async Task<bool> InsightExistsForMonthAsync(
        long userId, string code, int year, int month, int? categoryId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",     userId,     DbType.Int64);
        p.Add("@Code",       code,       DbType.String);
        p.Add("@Year",       year,       DbType.Int32);
        p.Add("@Month",      month,      DbType.Int32);
        p.Add("@CategoryId", categoryId, DbType.Int32);
        var result = await db.ExecuteScalarAsync<int>("MyMoney.usp_FIL_Insight_ExistsForMonth", p, ct);
        return result > 0;
    }

    public async Task CleanupExpiredInsightsAsync(CancellationToken ct = default)
    {
        await db.ExecuteAsync("MyMoney.usp_FIL_Insight_CleanupExpired", new DynamicParameters(), ct);
    }

    // ── Spending Patterns ─────────────────────────────────────────────────────

    public async Task UpsertPatternAsync(CreatePatternDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",          model.UserId,          DbType.Int64);
        p.Add("@PatternType",     model.PatternType,     DbType.Byte);
        p.Add("@Code",            model.Code,            DbType.String);
        p.Add("@DescriptionEn",   model.DescriptionEn,   DbType.String);
        p.Add("@DescriptionAr",   model.DescriptionAr,   DbType.String);
        p.Add("@ConfidenceScore", model.ConfidenceScore, DbType.Decimal);
        p.Add("@DataPointJson",   model.DataPointJson,   DbType.String);
        p.Add("@ValidUntilUtc",   model.ValidUntilUtc,   DbType.DateTime2);
        await db.ExecuteAsync("MyMoney.usp_FIL_Pattern_Upsert", p, ct);
    }

    public async Task<IReadOnlyList<PatternDbResult>> GetPatternsAsync(long userId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId", userId, DbType.Int64);
        return await db.QueryListAsync<PatternDbResult>("MyMoney.usp_FIL_Pattern_GetByUser", p, ct);
    }

    // ── Recommendations ───────────────────────────────────────────────────────

    public async Task<long> CreateRecommendationAsync(
        CreateRecommendationDbModel model,
        CancellationToken           ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",              model.UserId,              DbType.Int64);
        p.Add("@Type",                model.Type,                DbType.Byte);
        p.Add("@Code",                model.Code,                DbType.String);
        p.Add("@TitleEn",             model.TitleEn,             DbType.String);
        p.Add("@TitleAr",             model.TitleAr,             DbType.String);
        p.Add("@MessageEn",           model.MessageEn,           DbType.String);
        p.Add("@MessageAr",           model.MessageAr,           DbType.String);
        p.Add("@ExpectedImpactValue", model.ExpectedImpactValue, DbType.Decimal);
        p.Add("@Priority",            model.Priority,            DbType.Byte);
        p.Add("@RelatedCategoryId",   model.RelatedCategoryId,   DbType.Int32);
        p.Add("@ExpiresAtUtc",        model.ExpiresAtUtc,        DbType.DateTime2);
        p.Add("@NewId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        await db.ExecuteAsync("MyMoney.usp_FIL_Recommendation_Create", p, ct);
        return p.Get<long>("@NewId");
    }

    public Task<GetRecommendationsDbResult> GetRecommendationsAsync(
        GetRecommendationsDbModel model,
        CancellationToken         ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",     model.UserId,     DbType.Int64);
        p.Add("@PageNumber", model.PageNumber, DbType.Int32);
        p.Add("@PageSize",   model.PageSize,   DbType.Int32);

        return db.QueryMultipleAsync(
            "MyMoney.usp_FIL_Recommendation_GetList",
            async multi =>
            {
                var items = (await multi.ReadAsync<RecommendationDbResult>()).AsList();
                var total = await multi.ReadFirstOrDefaultAsync<RecommendationCountRow>()
                            ?? new RecommendationCountRow(0);
                return new GetRecommendationsDbResult
                {
                    Items      = items,
                    TotalCount = total.TotalCount
                };
            },
            p, ct);
    }

    public async Task<int> MarkRecommendationAppliedAsync(
        long userId, long recommendationId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",           userId,           DbType.Int64);
        p.Add("@RecommendationId", recommendationId, DbType.Int64);
        p.Add("@RowsAffected", dbType: DbType.Int32, direction: ParameterDirection.Output);
        await db.ExecuteAsync("MyMoney.usp_FIL_Recommendation_MarkApplied", p, ct);
        return p.Get<int>("@RowsAffected");
    }

    public async Task<int> DismissRecommendationAsync(
        long userId, long recommendationId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",           userId,           DbType.Int64);
        p.Add("@RecommendationId", recommendationId, DbType.Int64);
        p.Add("@RowsAffected", dbType: DbType.Int32, direction: ParameterDirection.Output);
        await db.ExecuteAsync("MyMoney.usp_FIL_Recommendation_Dismiss", p, ct);
        return p.Get<int>("@RowsAffected");
    }

    public async Task<bool> RecommendationExistsForMonthAsync(
        long userId, string code, int year, int month, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId", userId, DbType.Int64);
        p.Add("@Code",   code,   DbType.String);
        p.Add("@Year",   year,   DbType.Int32);
        p.Add("@Month",  month,  DbType.Int32);
        var result = await db.ExecuteScalarAsync<int>("MyMoney.usp_FIL_Recommendation_ExistsForMonth", p, ct);
        return result > 0;
    }

    public async Task CleanupExpiredRecommendationsAsync(CancellationToken ct = default)
    {
        await db.ExecuteAsync("MyMoney.usp_FIL_Recommendation_Cleanup", new DynamicParameters(), ct);
    }

    // ── Large-transaction detection ───────────────────────────────────────────

    public async Task<IReadOnlyList<LargeTransactionDbResult>> GetRecentLargeTransactionsAsync(
        DateTime fromUtc, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@FromUtc", fromUtc, DbType.DateTime2);
        return await db.QueryListAsync<LargeTransactionDbResult>(
            "MyMoney.usp_FIL_Transaction_GetLargeRecent", p, ct);
    }

    // ── Active users ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ActiveUserDbResult>> GetActiveUsersAsync(
        int activeDays, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@ActiveDays", activeDays, DbType.Int32);
        return await db.QueryListAsync<ActiveUserDbResult>(
            "MyMoney.usp_FIL_User_GetActive", p, ct);
    }

    // ── Private result records ────────────────────────────────────────────────

    private sealed record InsightCountsRow(int TotalCount, int UnreadCount);
    private sealed record RecommendationCountRow(int TotalCount);
}
