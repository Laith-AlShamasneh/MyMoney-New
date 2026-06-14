namespace Application.Features.Reports.Jobs;

public sealed record GenerateReportPayload(
    long   ReportId,
    long   UserId,
    string ReportTypeKey,
    string Language,
    string DateFrom,
    string DateTo,
    string UserEmail,
    string UserDisplayName);
