using Application.Features.Reports.DbModels;

namespace Application.Interfaces.Repositories;

public interface IReportRepository
{
    Task<IReadOnlyList<ReportTypeDbModel>> GetTypesAsync(CancellationToken ct = default);

    Task<long> CreateAsync(
        long      userId,
        long?     workspaceId,
        byte      reportTypeId,
        string    reportTypeKey,
        string    language,
        DateOnly  dateFrom,
        DateOnly  dateTo,
        DateTime  expiresOnUtc,
        CancellationToken ct = default);

    Task UpdateToProcessingAsync(long reportId, CancellationToken ct = default);

    Task CompleteAsync(long reportId, string filePath, long fileSize, CancellationToken ct = default);

    Task FailAsync(long reportId, string errorMessage, CancellationToken ct = default);

    Task<ReportDbModel?> GetByIdAsync(long reportId, long userId, long? workspaceId, CancellationToken ct = default);

    Task<IReadOnlyList<ReportDbModel>> GetListAsync(long userId, long? workspaceId, CancellationToken ct = default);

    Task<bool> DeleteAsync(long reportId, long userId, long? workspaceId, CancellationToken ct = default);

    Task ExpireOldAsync(CancellationToken ct = default);
}
