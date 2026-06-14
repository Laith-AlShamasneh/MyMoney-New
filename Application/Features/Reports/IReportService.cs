using Application.Features.Reports.DTOs;
using Shared.Results;

namespace Application.Features.Reports;

public interface IReportService
{
    Task<ServiceResult<IReadOnlyList<ReportTypeDto>>> GetTypesAsync(CancellationToken ct = default);
    Task<ServiceResult<GenerateReportResponse>> GenerateAsync(GenerateReportRequest request, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<ReportDto>>> GetListAsync(CancellationToken ct = default);
    Task<ServiceResult<(string FileName, byte[] Content, string ContentType)>> DownloadAsync(long reportId, CancellationToken ct = default);
    Task<ServiceResult<object?>> DeleteAsync(long reportId, CancellationToken ct = default);
}
