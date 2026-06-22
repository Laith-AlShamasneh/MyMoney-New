using Application.Features.Receipt.DTOs;
using Shared.Responses;
using Shared.Results;

namespace Application.Features.Receipt;

public interface IReceiptService
{
    Task<ServiceResult<UploadReceiptResponse>>                    UploadAsync(UploadReceiptRequest request, CancellationToken ct);
    Task<ServiceResult<ReceiptDetailResponse>>                    GetByIdAsync(GetReceiptRequest request, CancellationToken ct);
    Task<ServiceResult<PagedResponse<ReceiptSummaryResponse>>>    SearchAsync(SearchReceiptsRequest request, CancellationToken ct);
    Task<ServiceResult<object?>>                                  UpdateAsync(UpdateReceiptRequest request, CancellationToken ct);
    Task<ServiceResult<object?>>                                  DeleteAsync(ReceiptActionRequest request, CancellationToken ct);
    Task<ServiceResult<object?>>                                  ArchiveAsync(ReceiptActionRequest request, CancellationToken ct);
    Task<ServiceResult<object?>>                                  RestoreAsync(ReceiptActionRequest request, CancellationToken ct);
    Task<ServiceResult<object?>>                                  AssignTransactionAsync(AssignTransactionRequest request, CancellationToken ct);
    Task<ServiceResult<ReceiptDashboardResponse>>                 GetDashboardAsync(CancellationToken ct);
    Task<ServiceResult<IReadOnlyList<ReceiptTagListResponse>>>    GetTagsAsync(CancellationToken ct);
    Task<ServiceResult<ReceiptTagListResponse>>                   CreateTagAsync(CreateReceiptTagRequest request, CancellationToken ct);
    Task<ServiceResult<object?>>                                  DeleteTagAsync(DeleteReceiptTagRequest request, CancellationToken ct);
    Task<ServiceResult<object?>>                                  SetReceiptTagsAsync(SetReceiptTagsRequest request, CancellationToken ct);
    Task<ServiceResult<DownloadReceiptResponse>>                  DownloadAsync(DownloadReceiptRequest request, CancellationToken ct);
}
