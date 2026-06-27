using Application.Common.Constants;
using Application.Features.Reports.DbModels;
using Application.Features.Reports.DTOs;
using Application.Features.Reports.Jobs;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Shared.Constants;
using Shared.Enums.Reporting;
using Shared.Enums.System;
using Shared.Results;

namespace Application.Features.Reports;

internal sealed class ReportService(
    IReportRepository    reportRepository,
    IBackgroundJobService backgroundJobService,
    IFileService         fileService,
    IUserContext         userContext,
    IMessageProvider     messageProvider,
    INotificationPublisher notificationPublisher) : IReportService
{
    private static readonly TimeSpan ReportExpiry = TimeSpan.FromDays(7);

    public async Task<ServiceResult<IReadOnlyList<ReportTypeDto>>> GetTypesAsync(
        CancellationToken ct = default)
    {
        var types = await reportRepository.GetTypesAsync(ct);
        var dtos  = types.Select(MapTypeDto).ToList();
        var msg   = await messageProvider.GetMessagesAsync(MessageKeys.Reports.TypesLoaded, ct);
        return ServiceResultFactory.Success<IReadOnlyList<ReportTypeDto>>(dtos, InternalResponseCodes.OK, msg);
    }

    public async Task<ServiceResult<GenerateReportResponse>> GenerateAsync(
        GenerateReportRequest request,
        CancellationToken     ct = default)
    {
        // Validate report type exists
        var types = await reportRepository.GetTypesAsync(ct);
        var type  = types.FirstOrDefault(t => t.Id == request.ReportTypeId);
        if (type is null)
        {
            return ServiceResultFactory.Failure<GenerateReportResponse>(
                InternalResponseCodes.BadRequest,
                await messageProvider.GetMessagesAsync(MessageKeys.Reports.InvalidReportType, ct));
        }

        var dateFrom = DateOnly.Parse(request.DateFrom);
        var dateTo   = DateOnly.Parse(request.DateTo);
        var expires  = DateTime.UtcNow.Add(ReportExpiry);

        var reportId = await reportRepository.CreateAsync(
            userContext.UserId, userContext.WorkspaceId, type.Id, type.Key, request.Language,
            dateFrom, dateTo, expires, ct);

        await backgroundJobService.EnqueueAsync(
            JobTypes.GenerateReport,
            new GenerateReportPayload(
                reportId,
                userContext.UserId,
                type.Key,
                request.Language,
                request.DateFrom,
                request.DateTo,
                userContext.Email,
                userContext.DisplayName,
                userContext.WorkspaceId),
            priority:    2,
            maxAttempts: 3,
            ct:          ct);

        //await notificationPublisher.PublishAsync(
        //    templateCode: NotificationCodes.ReportReady,
        //    userId: userContext.UserId,
        //    parameters: new Dictionary<string, string>
        //    {
        //        { "ReportType", type.Key }
        //    },
        //    payload: new
        //    {
        //        ReportId = reportId,
        //        Type = type.Key
        //    },
        //    ct: ct);
        var msg = await messageProvider.GetMessagesAsync(MessageKeys.Reports.Generated, ct);
        return ServiceResultFactory.Success(new GenerateReportResponse(reportId), InternalResponseCodes.Created, msg);
    }

    public async Task<ServiceResult<IReadOnlyList<ReportDto>>> GetListAsync(
        CancellationToken ct = default)
    {
        var reports = await reportRepository.GetListAsync(userContext.UserId, userContext.WorkspaceId, ct);
        var dtos    = reports.Select(MapDto).ToList();
        var msg     = await messageProvider.GetMessagesAsync(MessageKeys.Reports.ListLoaded, ct);
        return ServiceResultFactory.Success<IReadOnlyList<ReportDto>>(dtos, InternalResponseCodes.OK, msg);
    }

    public async Task<ServiceResult<(string FileName, byte[] Content, string ContentType)>> DownloadAsync(
        long              reportId,
        CancellationToken ct = default)
    {
        var report = await reportRepository.GetByIdAsync(reportId, userContext.UserId, userContext.WorkspaceId, ct);

        if (report is null)
        {
            return ServiceResultFactory.Failure<(string, byte[], string)>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Reports.NotFound, ct));
        }

        if (report.Status != (byte)ReportStatus.Completed || string.IsNullOrEmpty(report.FilePath))
        {
            return ServiceResultFactory.Failure<(string, byte[], string)>(
                InternalResponseCodes.BadRequest,
                await messageProvider.GetMessagesAsync(MessageKeys.Reports.NotReady, ct));
        }

        var stream  = await fileService.DownloadAsync(report.FilePath, ct);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var content = ms.ToArray();

        var typeName = report.Language == "ar" ? report.ReportTypeNameAr : report.ReportTypeNameEn;
        var fileName = $"{typeName}_{report.DateFrom:yyyy-MM-dd}_{report.DateTo:yyyy-MM-dd}.xlsx"
            .Replace(" ", "_");

        var msg = await messageProvider.GetMessagesAsync(MessageKeys.Reports.DownloadReady, ct);
        return ServiceResultFactory.Success(
            (fileName, content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
            InternalResponseCodes.OK,
            msg);
    }

    public async Task<ServiceResult<object?>> DeleteAsync(
        long              reportId,
        CancellationToken ct = default)
    {
        var existing = await reportRepository.GetByIdAsync(reportId, userContext.UserId, userContext.WorkspaceId, ct);
        if (existing is null)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Reports.NotFound, ct));
        }

        await reportRepository.DeleteAsync(reportId, userContext.UserId, userContext.WorkspaceId, ct);

        if (!string.IsNullOrEmpty(existing.FilePath))
        {
            try { await fileService.DeleteAsync(existing.FilePath, ct); } catch { /* file may already be gone */ }
        }

        var msg = await messageProvider.GetMessagesAsync(MessageKeys.Reports.Deleted, ct);
        return ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK, msg);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static ReportTypeDto MapTypeDto(ReportTypeDbModel t) =>
        new(t.Id, t.Key, t.NameEn, t.NameAr, t.DescriptionEn, t.DescriptionAr);

    private static ReportDto MapDto(ReportDbModel r) =>
        new(r.Id,
            r.ReportTypeId,
            r.ReportTypeKey,
            r.ReportTypeNameEn,
            r.ReportTypeNameAr,
            r.Status,
            StatusName(r.Status),
            r.Language,
            r.DateFrom.ToString("yyyy-MM-dd"),
            r.DateTo.ToString("yyyy-MM-dd"),
            r.FileSize,
            r.RequestedOnUtc,
            r.CompletedOnUtc,
            r.ExpiresOnUtc,
            CanDownload: r.Status == (byte)ReportStatus.Completed,
            CanDelete:   r.Status != (byte)ReportStatus.Processing);

    private static string StatusName(byte status) => (ReportStatus)status switch
    {
        ReportStatus.Pending    => "Pending",
        ReportStatus.Processing => "Processing",
        ReportStatus.Completed  => "Completed",
        ReportStatus.Failed     => "Failed",
        ReportStatus.Expired    => "Expired",
        _                       => "Unknown"
    };
}
