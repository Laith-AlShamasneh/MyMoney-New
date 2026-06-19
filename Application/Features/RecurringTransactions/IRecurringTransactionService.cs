using Application.Features.RecurringTransactions.DTOs;
using Shared.Responses;
using Shared.Results;

namespace Application.Features.RecurringTransactions;

public interface IRecurringTransactionService
{
    // Recurring transaction CRUD
    Task<ServiceResult<RecurringTransactionResponse>>                        CreateAsync(CreateRecurringTransactionRequest request, CancellationToken ct = default);
    Task<ServiceResult<RecurringTransactionResponse>>                        GetByIdAsync(long id, CancellationToken ct = default);
    Task<ServiceResult<PagedResponse<RecurringTransactionListItemResponse>>> GetListAsync(GetRecurringTransactionsRequest request, CancellationToken ct = default);
    Task<ServiceResult<RecurringTransactionResponse>>                        UpdateAsync(UpdateRecurringTransactionRequest request, CancellationToken ct = default);
    Task<ServiceResult<object?>>                                             DeleteAsync(long id, CancellationToken ct = default);
    Task<ServiceResult<object?>>                                             PauseAsync(long id, CancellationToken ct = default);
    Task<ServiceResult<object?>>                                             ResumeAsync(long id, CancellationToken ct = default);
    Task<ServiceResult<RecurringTransactionDashboardResponse>>               GetDashboardAsync(CancellationToken ct = default);

    // Subscription operations (same underlying data — filtered to IsSubscription = true)
    Task<ServiceResult<SubscriptionResponse>>                                CreateSubscriptionAsync(CreateSubscriptionRequest request, CancellationToken ct = default);
    Task<ServiceResult<PagedResponse<SubscriptionListItemResponse>>>         GetSubscriptionsAsync(GetSubscriptionsRequest request, CancellationToken ct = default);
    Task<ServiceResult<SubscriptionResponse>>                                UpdateSubscriptionAsync(UpdateSubscriptionRequest request, CancellationToken ct = default);
}
