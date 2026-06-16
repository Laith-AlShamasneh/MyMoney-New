namespace Application.Features.Reports.DTOs;

public sealed record GenerateReportRequest(
    byte   ReportTypeId,
    string Language,
    string DateFrom,
    string DateTo);

public sealed record ReportTypeDto(
    byte   Id,
    string Key,
    string NameEn,
    string NameAr,
    string DescriptionEn,
    string DescriptionAr);

public sealed record ReportDto(
    long      Id,
    byte      ReportTypeId,
    string    ReportTypeKey,
    string    ReportTypeNameEn,
    string    ReportTypeNameAr,
    byte      Status,
    string    StatusName,
    string    Language,
    string    DateFrom,
    string    DateTo,
    long?     FileSize,
    DateTime  RequestedOnUtc,
    DateTime? CompletedOnUtc,
    DateTime? ExpiresOnUtc,
    bool      CanDownload,
    bool      CanDelete);

public sealed record GenerateReportResponse(long ReportId);

public sealed record DeleteReportRequest(long Id);
