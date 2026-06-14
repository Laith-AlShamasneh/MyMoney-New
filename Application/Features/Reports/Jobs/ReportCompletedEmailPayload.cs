namespace Application.Features.Reports.Jobs;

public sealed record ReportCompletedEmailPayload(
    string To,
    string Language,
    string UserDisplayName,
    string ReportTypeNameEn,
    string ReportTypeNameAr,
    string DateFrom,
    string DateTo,
    string GeneratedOn);
