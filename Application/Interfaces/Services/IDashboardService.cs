using Application.Features.Dashboard.DTOs;
using Shared.Results;

namespace Application.Interfaces.Services;

public interface IDashboardService
{
    /// <param name="period">0 = current month (with vs-last-month comparison), 1 = all time.</param>
    Task<ServiceResult<DashboardSummaryResponse>> GetSummaryAsync(byte period = 0, CancellationToken ct = default);
}
