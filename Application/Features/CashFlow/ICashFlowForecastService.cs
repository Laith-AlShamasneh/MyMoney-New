using Application.Features.CashFlow.DTOs;
using Shared.Results;

namespace Application.Features.CashFlow;

public interface ICashFlowForecastService
{
    Task<ServiceResult<CashFlowForecastResponse>>  GetForecastAsync(GetForecastRequest request, CancellationToken ct = default);
    Task<ServiceResult<CashFlowDashboardResponse>> GetDashboardAsync(CancellationToken ct = default);
}
