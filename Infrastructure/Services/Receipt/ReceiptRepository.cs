using Application.Features.Receipt.DbModels;
using Application.Interfaces.Database;
using Application.Interfaces.Repositories;
using Dapper;
using System.Data;

namespace Infrastructure.Services.Receipt;

internal sealed class ReceiptRepository(IDbExecutor db) : IReceiptRepository
{
    // ── Upload ────────────────────────────────────────────────────────────────

    public async Task<UploadReceiptDbResult> UploadAsync(UploadReceiptDbModel model, CancellationToken ct)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",           model.UserId,           DbType.Int64);
        p.Add("@WorkspaceId",      model.WorkspaceId,      DbType.Int64);
        p.Add("@OriginalFileName", model.OriginalFileName, DbType.String);
        p.Add("@StoredFileName",   model.StoredFileName,   DbType.String);
        p.Add("@FileExtension",    model.FileExtension,    DbType.String);
        p.Add("@FileSizeBytes",    model.FileSizeBytes,    DbType.Int64);
        p.Add("@ContentType",      model.ContentType,      DbType.String);
        p.Add("@FileHash",         model.FileHash,         DbType.String);
        p.Add("@Title",            model.Title,            DbType.String);
        p.Add("@Description",      model.Description,      DbType.String);
        p.Add("@ReceiptDate",      model.ReceiptDate,      DbType.Date);
        p.Add("@MerchantName",     model.MerchantName,     DbType.String);
        p.Add("@Amount",           model.Amount,           DbType.Decimal);
        p.Add("@CurrencyCode",     model.CurrencyCode,     DbType.String);
        p.Add("@Notes",            model.Notes,            DbType.String);
        p.Add("@ReceiptId",        dbType: DbType.Int64,    direction: ParameterDirection.Output);
        p.Add("@IsDuplicate",      dbType: DbType.Boolean,  direction: ParameterDirection.Output);
        p.Add("@DuplicateId",      dbType: DbType.Int64,    direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Receipt_Upload", p, ct);

        return new UploadReceiptDbResult
        {
            ReceiptId   = p.Get<long>("@ReceiptId"),
            IsDuplicate = p.Get<bool>("@IsDuplicate"),
            DuplicateId = p.Get<long>("@DuplicateId")
        };
    }

    // ── GetById ───────────────────────────────────────────────────────────────

    public async Task<ReceiptWithTagsDbResult> GetByIdAsync(long userId, long? workspaceId, long receiptId, CancellationToken ct)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",      userId,      DbType.Int64);
        p.Add("@WorkspaceId", workspaceId, DbType.Int64);
        p.Add("@ReceiptId",   receiptId,   DbType.Int64);

        return await db.QueryMultipleAsync(
            "MyMoney.usp_Receipt_GetById",
            async multi =>
            {
                var receipt = await multi.ReadFirstOrDefaultAsync<ReceiptDetailDbResult>();
                var tags    = (await multi.ReadAsync<ReceiptTagDbResult>()).AsList();

                return new ReceiptWithTagsDbResult
                {
                    Receipt = receipt,
                    Tags    = tags
                };
            },
            p, ct);
    }

    // ── Search ────────────────────────────────────────────────────────────────

    public async Task<SearchReceiptsDbResult> SearchAsync(SearchReceiptsDbModel model, CancellationToken ct)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",     model.UserId,                          DbType.Int64);
        p.Add("@WorkspaceId", model.WorkspaceId,                    DbType.Int64);
        p.Add("@Keyword",    model.Keyword,                         DbType.String);
        p.Add("@StatusId",   model.StatusId.HasValue
                                 ? (byte?)model.StatusId : null,   DbType.Byte);
        p.Add("@DateFrom",   model.DateFrom,                        DbType.Date);
        p.Add("@DateTo",     model.DateTo,                          DbType.Date);
        p.Add("@AmountMin",  model.AmountMin,                       DbType.Decimal);
        p.Add("@AmountMax",  model.AmountMax,                       DbType.Decimal);
        p.Add("@TagId",      model.TagId,                           DbType.Int32);
        p.Add("@PageNumber", model.PageNumber,                      DbType.Int32);
        p.Add("@PageSize",   model.PageSize,                        DbType.Int32);
        p.Add("@TotalCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

        var items = await db.QueryListAsync<ReceiptSummaryDbResult>(
            "MyMoney.usp_Receipt_Search", p, ct);

        return new SearchReceiptsDbResult
        {
            Items      = items,
            TotalCount = p.Get<int>("@TotalCount")
        };
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public async Task<int> UpdateAsync(UpdateReceiptDbModel model, CancellationToken ct)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",       model.UserId,       DbType.Int64);
        p.Add("@WorkspaceId",  model.WorkspaceId,  DbType.Int64);
        p.Add("@ReceiptId",    model.ReceiptId,    DbType.Int64);
        p.Add("@Title",        model.Title,        DbType.String);
        p.Add("@Description",  model.Description,  DbType.String);
        p.Add("@ReceiptDate",  model.ReceiptDate,  DbType.Date);
        p.Add("@MerchantName", model.MerchantName, DbType.String);
        p.Add("@Amount",       model.Amount,       DbType.Decimal);
        p.Add("@CurrencyCode", model.CurrencyCode, DbType.String);
        p.Add("@Notes",        model.Notes,        DbType.String);
        p.Add("@RowsAffected", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Receipt_Update", p, ct);

        return p.Get<int>("@RowsAffected");
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    public async Task<DeleteReceiptDbResult> DeleteAsync(long userId, long? workspaceId, long receiptId, CancellationToken ct)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",            userId,      DbType.Int64);
        p.Add("@WorkspaceId",       workspaceId, DbType.Int64);
        p.Add("@ReceiptId",         receiptId, DbType.Int64);
        p.Add("@StoredFileName",    dbType: DbType.String,  size: 500, direction: ParameterDirection.Output);
        p.Add("@ThumbnailFileName", dbType: DbType.String,  size: 500, direction: ParameterDirection.Output);
        p.Add("@RowsAffected",      dbType: DbType.Int32,   direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Receipt_Delete", p, ct);

        return new DeleteReceiptDbResult
        {
            StoredFileName    = p.Get<string?>("@StoredFileName"),
            ThumbnailFileName = p.Get<string?>("@ThumbnailFileName"),
            RowsAffected      = p.Get<int>("@RowsAffected")
        };
    }

    // ── Archive / Restore ─────────────────────────────────────────────────────

    public async Task<int> ArchiveAsync(long userId, long? workspaceId, long receiptId, CancellationToken ct)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",       userId,      DbType.Int64);
        p.Add("@WorkspaceId",  workspaceId, DbType.Int64);
        p.Add("@ReceiptId",    receiptId, DbType.Int64);
        p.Add("@RowsAffected", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Receipt_Archive", p, ct);

        return p.Get<int>("@RowsAffected");
    }

    public async Task<int> RestoreAsync(long userId, long? workspaceId, long receiptId, CancellationToken ct)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",       userId,      DbType.Int64);
        p.Add("@WorkspaceId",  workspaceId, DbType.Int64);
        p.Add("@ReceiptId",    receiptId, DbType.Int64);
        p.Add("@RowsAffected", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Receipt_Restore", p, ct);

        return p.Get<int>("@RowsAffected");
    }

    // ── Assign Transaction ────────────────────────────────────────────────────

    public async Task<int> AssignTransactionAsync(long userId, long? workspaceId, long receiptId, long? transactionId, CancellationToken ct)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",        userId,        DbType.Int64);
        p.Add("@WorkspaceId",   workspaceId,   DbType.Int64);
        p.Add("@ReceiptId",     receiptId,     DbType.Int64);
        p.Add("@TransactionId", transactionId, DbType.Int64);
        p.Add("@RowsAffected",  dbType: DbType.Int32, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Receipt_AssignTransaction", p, ct);

        return p.Get<int>("@RowsAffected");
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    public async Task<ReceiptDashboardDbResult> GetDashboardAsync(long userId, long? workspaceId, CancellationToken ct)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",      userId,      DbType.Int64);
        p.Add("@WorkspaceId", workspaceId, DbType.Int64);

        return await db.QueryMultipleAsync(
            "MyMoney.usp_Receipt_GetDashboard",
            async multi =>
            {
                var summary = await multi.ReadFirstOrDefaultAsync<ReceiptDashboardSummaryDbResult>()
                              ?? new ReceiptDashboardSummaryDbResult();
                var recent  = (await multi.ReadAsync<ReceiptSummaryDbResult>()).AsList();

                return new ReceiptDashboardDbResult
                {
                    Summary = summary,
                    Recent  = recent
                };
            },
            p, ct);
    }

    // ── Tags ──────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ReceiptTagListDbResult>> GetTagListAsync(long userId, long? workspaceId, CancellationToken ct)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",      userId,      DbType.Int64);
        p.Add("@WorkspaceId", workspaceId, DbType.Int64);

        return await db.QueryListAsync<ReceiptTagListDbResult>(
            "MyMoney.usp_Receipt_Tag_GetList", p, ct);
    }

    public async Task<CreateReceiptTagDbResult> CreateTagAsync(CreateReceiptTagDbModel model, CancellationToken ct)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",      model.UserId,      DbType.Int64);
        p.Add("@WorkspaceId", model.WorkspaceId, DbType.Int64);
        p.Add("@Name",        model.Name,     DbType.String);
        p.Add("@ColorHex",    model.ColorHex, DbType.String);
        p.Add("@TagId",       dbType: DbType.Int32,   direction: ParameterDirection.Output);
        p.Add("@IsDuplicate", dbType: DbType.Boolean, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Receipt_Tag_Create", p, ct);

        return new CreateReceiptTagDbResult
        {
            TagId       = p.Get<int>("@TagId"),
            IsDuplicate = p.Get<bool>("@IsDuplicate")
        };
    }

    public async Task<int> DeleteTagAsync(long userId, long? workspaceId, int tagId, CancellationToken ct)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",       userId,      DbType.Int64);
        p.Add("@WorkspaceId",  workspaceId, DbType.Int64);
        p.Add("@TagId",        tagId,  DbType.Int32);
        p.Add("@RowsAffected", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Receipt_Tag_Delete", p, ct);

        return p.Get<int>("@RowsAffected");
    }

    public async Task SetTagsAsync(long userId, long? workspaceId, long receiptId, string tagIdsJson, CancellationToken ct)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",      userId,      DbType.Int64);
        p.Add("@WorkspaceId", workspaceId, DbType.Int64);
        p.Add("@ReceiptId", receiptId,  DbType.Int64);
        p.Add("@TagIds",    tagIdsJson, DbType.String);

        await db.ExecuteAsync("MyMoney.usp_Receipt_Tags_Set", p, ct);
    }

    // ── OCR ───────────────────────────────────────────────────────────────────

    public async Task<long> SaveOcrResultAsync(
        long     receiptId,
        string?  rawText,
        string?  merchantName,
        decimal? totalAmount,
        DateOnly? receiptDate,
        decimal? confidence,
        string   providerName,
        string?  errorMessage,
        bool     isSuccess,
        CancellationToken ct)
    {
        var p = new DynamicParameters();
        p.Add("@ReceiptId",    receiptId,    DbType.Int64);
        p.Add("@RawText",      rawText,      DbType.String);
        p.Add("@MerchantName", merchantName, DbType.String);
        p.Add("@TotalAmount",  totalAmount,  DbType.Decimal);
        p.Add("@ReceiptDate",  receiptDate,  DbType.Date);
        p.Add("@Confidence",   confidence,   DbType.Decimal);
        p.Add("@ProviderName", providerName, DbType.String);
        p.Add("@ErrorMessage", errorMessage, DbType.String);
        p.Add("@IsSuccess",    isSuccess,    DbType.Boolean);
        p.Add("@OcrResultId",  dbType: DbType.Int64, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Receipt_OcrResult_Save", p, ct);

        return p.Get<long>("@OcrResultId");
    }

    public async Task<int> SetProcessingStatusAsync(long receiptId, byte processingStatusId, CancellationToken ct)
    {
        var p = new DynamicParameters();
        p.Add("@ReceiptId",          receiptId,          DbType.Int64);
        p.Add("@ProcessingStatusId", processingStatusId, DbType.Byte);
        p.Add("@RowsAffected",       dbType: DbType.Int32, direction: ParameterDirection.Output);

        await db.ExecuteAsync("MyMoney.usp_Receipt_SetProcessingStatus", p, ct);

        return p.Get<int>("@RowsAffected");
    }

    public async Task<IReadOnlyList<PendingOcrReceiptDbResult>> GetPendingOcrAsync(int batchSize, CancellationToken ct)
    {
        var p = new DynamicParameters();
        p.Add("@BatchSize", batchSize, DbType.Int32);

        return await db.QueryListAsync<PendingOcrReceiptDbResult>(
            "MyMoney.usp_Receipt_GetPendingOcr", p, ct);
    }
}
