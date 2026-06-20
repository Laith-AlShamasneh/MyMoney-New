using Application.Features.Budget.DTOs;
using Shared.Results;

namespace Application.Features.Budget;

public interface IBudgetService
{
    Task<ServiceResult<IReadOnlyList<BudgetResponse>>> GetListAsync(int? statusId, CancellationToken ct = default);
    Task<ServiceResult<BudgetDetailResponse>>          GetByIdAsync(long budgetId, CancellationToken ct = default);
    Task<ServiceResult<BudgetDashboardResponse>>       GetDashboardAsync(CancellationToken ct = default);
    Task<ServiceResult<BudgetResponse>>                CreateAsync(CreateBudgetRequest request, CancellationToken ct = default);
    Task<ServiceResult<BudgetResponse>>                UpdateAsync(UpdateBudgetRequest request, CancellationToken ct = default);
    Task<ServiceResult<object?>>                       DeleteAsync(long budgetId, CancellationToken ct = default);
    Task<ServiceResult<object?>>                       PauseAsync(long budgetId, CancellationToken ct = default);
    Task<ServiceResult<object?>>                       ResumeAsync(long budgetId, CancellationToken ct = default);
    Task<ServiceResult<BudgetPeriodListResponse>>      GetPeriodsAsync(long budgetId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<BudgetAnalyticsItem>>> GetAnalyticsAsync(GetBudgetAnalyticsRequest request, CancellationToken ct = default);
}
