using Application.Features.Reports;
using Application.Features.Reports.DTOs;
using WebApi.Common.Extensions;
using WebApi.Common.Filters;

namespace WebApi.Features.Report;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("api/reports")
                       .WithTags("Reports")
                       .RequireAuthorization();

        group.MapGet("types", async (
            IReportService    service,
            CancellationToken ct) =>
        {
            var result = await service.GetTypesAsync(ct);
            return result.ToHttpResponse();
        });

        group.MapPost("generate", async (
            GenerateReportRequest request,
            IReportService        service,
            CancellationToken     ct) =>
        {
            var result = await service.GenerateAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GenerateReportRequest>>();

        group.MapGet("list", async (
            IReportService    service,
            CancellationToken ct) =>
        {
            var result = await service.GetListAsync(ct);
            return result.ToHttpResponse();
        });

        group.MapGet("download/{id:long}", async (
            long              id,
            IReportService    service,
            CancellationToken ct) =>
        {
            var result = await service.DownloadAsync(id, ct);
            if (!result.IsSuccess)
                return result.ToHttpResponse();

            var (fileName, content, contentType) = result.Data;
            return Results.File(content, contentType, fileName);
        });

        group.MapDelete("delete/{id:long}", async (
            long              id,
            IReportService    service,
            CancellationToken ct) =>
        {
            var result = await service.DeleteAsync(id, ct);
            return result.ToHttpResponse();
        });
    }
}
