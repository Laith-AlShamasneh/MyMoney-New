using Application.Features.Goals.DTOs;
using Shared.Results;

namespace Application.Features.Goals;

public interface IGoalService
{
    Task<ServiceResult<GoalListResponse>>        GetListAsync(GetGoalListRequest request, CancellationToken ct = default);
    Task<ServiceResult<GoalDetailResponse>>      GetByIdAsync(long goalId, CancellationToken ct = default);
    Task<ServiceResult<CreateGoalResponse>>      CreateAsync(CreateGoalRequest request, CancellationToken ct = default);
    Task<ServiceResult<object?>>                 UpdateAsync(UpdateGoalRequest request, CancellationToken ct = default);
    Task<ServiceResult<object?>>                 DeleteAsync(long goalId, CancellationToken ct = default);
    Task<ServiceResult<object?>>                 PauseAsync(long goalId, CancellationToken ct = default);
    Task<ServiceResult<object?>>                 ResumeAsync(long goalId, CancellationToken ct = default);
    Task<ServiceResult<AddContributionResponse>> AddContributionAsync(AddContributionRequest request, CancellationToken ct = default);
    Task<ServiceResult<AddContributionResponse>> WithdrawAsync(WithdrawRequest request, CancellationToken ct = default);
    Task<ServiceResult<AddContributionResponse>> AdjustAsync(AdjustGoalRequest request, CancellationToken ct = default);
    Task<ServiceResult<ContributionListResponse>> GetContributionsAsync(GetContributionsRequest request, CancellationToken ct = default);
    Task<ServiceResult<GoalDashboardResponse>>   GetDashboardAsync(CancellationToken ct = default);
    Task<ServiceResult<object?>>                 LinkRecurringAsync(LinkRecurringRequest request, CancellationToken ct = default);
    Task<ServiceResult<object?>>                 UnlinkRecurringAsync(UnlinkRecurringRequest request, CancellationToken ct = default);

    // Called by background job handlers
    Task SyncAutoContributionsAsync(DateOnly date, CancellationToken ct = default);
    Task CheckBehindScheduleAsync(CancellationToken ct = default);
}
