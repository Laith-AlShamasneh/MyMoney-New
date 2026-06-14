using Application.Features.Transaction.DTOs;
using Shared.Results;

namespace Application.Interfaces.Services;

public interface ITransactionService
{
    Task<ServiceResult<TransactionSearchResponse>>    SearchAsync(SearchTransactionsRequest request, CancellationToken ct = default);
    Task<ServiceResult<TransactionAnalyticsResponse>> GetAnalyticsAsync(GetAnalyticsRequest request, CancellationToken ct = default);
    Task<ServiceResult<TransactionDetailResponse>>    GetByIdAsync(long transactionId, CancellationToken ct = default);
    Task<ServiceResult<CreateTransactionResponse>>    CreateAsync(CreateTransactionRequest request, CancellationToken ct = default);
    Task<ServiceResult<object?>>                      UpdateAsync(long transactionId, UpdateTransactionRequest request, CancellationToken ct = default);
    Task<ServiceResult<object?>>                      DeleteAsync(long transactionId, CancellationToken ct = default);
}
