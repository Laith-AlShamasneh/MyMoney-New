using Application.Features.Transaction.DbModels;

namespace Application.Interfaces.Repositories;

public interface ITransactionRepository
{
    Task<TransactionSearchDbResult>     SearchAsync(TransactionSearchDbModel model, CancellationToken ct = default);
    Task<TransactionAnalyticsDbResult>  GetAnalyticsAsync(TransactionAnalyticsDbModel model, CancellationToken ct = default);
    Task<TransactionByIdDbResult?>      GetByIdAsync(long userId, long transactionId, CancellationToken ct = default);
    Task<long>                          CreateAsync(CreateTransactionDbModel model, CancellationToken ct = default);
    Task<int>                           UpdateAsync(UpdateTransactionDbModel model, CancellationToken ct = default);
    Task<int>                           DeleteAsync(DeleteTransactionDbModel model, CancellationToken ct = default);
}
