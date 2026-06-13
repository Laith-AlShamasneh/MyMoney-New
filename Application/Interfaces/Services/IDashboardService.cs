using Application.Features.Dashboard.DTOs;
using Shared.Results;

namespace Application.Interfaces.Services;

public interface IDashboardService
{
    Task<ServiceResult<DashboardSummaryResponse>> GetSummaryAsync(CancellationToken ct = default);
}
