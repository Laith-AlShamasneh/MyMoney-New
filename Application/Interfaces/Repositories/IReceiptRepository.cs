using Application.Features.Receipt.DbModels;

namespace Application.Interfaces.Repositories;

public interface IReceiptRepository
{
    Task<UploadReceiptDbResult>           UploadAsync(UploadReceiptDbModel model, CancellationToken ct);
    Task<ReceiptWithTagsDbResult>         GetByIdAsync(long userId, long receiptId, CancellationToken ct);
    Task<SearchReceiptsDbResult>          SearchAsync(SearchReceiptsDbModel model, CancellationToken ct);
    Task<int>                             UpdateAsync(UpdateReceiptDbModel model, CancellationToken ct);
    Task<DeleteReceiptDbResult>           DeleteAsync(long userId, long receiptId, CancellationToken ct);
    Task<int>                             ArchiveAsync(long userId, long receiptId, CancellationToken ct);
    Task<int>                             RestoreAsync(long userId, long receiptId, CancellationToken ct);
    Task<int>                             AssignTransactionAsync(long userId, long receiptId, long? transactionId, CancellationToken ct);
    Task<ReceiptDashboardDbResult>        GetDashboardAsync(long userId, CancellationToken ct);
    Task<IReadOnlyList<ReceiptTagListDbResult>> GetTagListAsync(long userId, CancellationToken ct);
    Task<CreateReceiptTagDbResult>        CreateTagAsync(CreateReceiptTagDbModel model, CancellationToken ct);
    Task<int>                             DeleteTagAsync(long userId, int tagId, CancellationToken ct);
    Task                                  SetTagsAsync(long userId, long receiptId, string tagIdsJson, CancellationToken ct);
    Task<long>                            SaveOcrResultAsync(long receiptId, string? rawText, string? merchantName, decimal? totalAmount, DateOnly? receiptDate, decimal? confidence, string providerName, string? errorMessage, bool isSuccess, CancellationToken ct);
    Task<int>                             SetProcessingStatusAsync(long receiptId, byte processingStatusId, CancellationToken ct);
    Task<IReadOnlyList<PendingOcrReceiptDbResult>> GetPendingOcrAsync(int batchSize, CancellationToken ct);
}
