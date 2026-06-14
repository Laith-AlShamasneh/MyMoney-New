namespace Application.Interfaces.Reports;

public interface IReportGenerator
{
    string ReportTypeKey { get; }

    Task<byte[]> GenerateAsync(
        long              userId,
        string            language,
        ReportParameters  parameters,
        CancellationToken ct = default);
}

public sealed record ReportParameters(DateOnly DateFrom, DateOnly DateTo);
