using Application.Features.Receipt;
using Application.Features.Receipt.DTOs;
using Microsoft.AspNetCore.Mvc;
using WebApi.Common.Extensions;
using WebApi.Common.Filters;

namespace WebApi.Features.Receipt;

public static class ReceiptEndpoints
{
    public static void MapReceiptEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/receipts")
                       .WithTags("Receipts")
                       .RequireAuthorization();

        // ── Core CRUD ────────────────────────────────────────────────────────
        group.MapPost("/upload", UploadAsync)
             .DisableAntiforgery()
             .AddEndpointFilter<ValidationFilter<UploadReceiptRequest>>();

        group.MapPost("/search", SearchAsync)
             .AddEndpointFilter<ValidationFilter<SearchReceiptsRequest>>();

        group.MapPost("/get", GetByIdAsync)
             .AddEndpointFilter<ValidationFilter<GetReceiptRequest>>();

        group.MapPost("/update", UpdateAsync)
             .AddEndpointFilter<ValidationFilter<UpdateReceiptRequest>>();

        group.MapPost("/delete", DeleteAsync)
             .AddEndpointFilter<ValidationFilter<ReceiptActionRequest>>();

        group.MapPost("/download", DownloadAsync)
             .AddEndpointFilter<ValidationFilter<DownloadReceiptRequest>>();

        // ── Lifecycle ────────────────────────────────────────────────────────
        group.MapPost("/archive", ArchiveAsync)
             .AddEndpointFilter<ValidationFilter<ReceiptActionRequest>>();

        group.MapPost("/restore", RestoreAsync)
             .AddEndpointFilter<ValidationFilter<ReceiptActionRequest>>();

        // ── Transaction link ─────────────────────────────────────────────────
        group.MapPost("/assign-transaction", AssignTransactionAsync)
             .AddEndpointFilter<ValidationFilter<AssignTransactionRequest>>();

        // ── Dashboard ────────────────────────────────────────────────────────
        group.MapPost("/dashboard", GetDashboardAsync);

        // ── Tags ─────────────────────────────────────────────────────────────
        group.MapPost("/tags/list", GetTagsAsync);

        group.MapPost("/tags/create", CreateTagAsync)
             .AddEndpointFilter<ValidationFilter<CreateReceiptTagRequest>>();

        group.MapPost("/tags/delete", DeleteTagAsync)
             .AddEndpointFilter<ValidationFilter<DeleteReceiptTagRequest>>();

        group.MapPost("/tags/set", SetReceiptTagsAsync)
             .AddEndpointFilter<ValidationFilter<SetReceiptTagsRequest>>();
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private static async Task<IResult> UploadAsync(
        [FromForm] UploadReceiptRequest request,
        IReceiptService                 service,
        CancellationToken               ct)
    {
        var result = await service.UploadAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> SearchAsync(
        SearchReceiptsRequest request,
        IReceiptService       service,
        CancellationToken     ct)
    {
        var result = await service.SearchAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> GetByIdAsync(
        GetReceiptRequest request,
        IReceiptService   service,
        CancellationToken ct)
    {
        var result = await service.GetByIdAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> UpdateAsync(
        UpdateReceiptRequest request,
        IReceiptService      service,
        CancellationToken    ct)
    {
        var result = await service.UpdateAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> DeleteAsync(
        ReceiptActionRequest request,
        IReceiptService      service,
        CancellationToken    ct)
    {
        var result = await service.DeleteAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> DownloadAsync(
        DownloadReceiptRequest request,
        IReceiptService        service,
        CancellationToken      ct)
    {
        var result = await service.DownloadAsync(request, ct);
        if (!result.IsSuccess)
            return result.ToHttpResponse();

        var download = result.Data!;
        return Results.Stream(
            download.Stream,
            download.ContentType,
            download.FileName,
            enableRangeProcessing: true);
    }

    private static async Task<IResult> ArchiveAsync(
        ReceiptActionRequest request,
        IReceiptService      service,
        CancellationToken    ct)
    {
        var result = await service.ArchiveAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> RestoreAsync(
        ReceiptActionRequest request,
        IReceiptService      service,
        CancellationToken    ct)
    {
        var result = await service.RestoreAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> AssignTransactionAsync(
        AssignTransactionRequest request,
        IReceiptService          service,
        CancellationToken        ct)
    {
        var result = await service.AssignTransactionAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> GetDashboardAsync(
        IReceiptService   service,
        CancellationToken ct)
    {
        var result = await service.GetDashboardAsync(ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> GetTagsAsync(
        IReceiptService   service,
        CancellationToken ct)
    {
        var result = await service.GetTagsAsync(ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> CreateTagAsync(
        CreateReceiptTagRequest request,
        IReceiptService         service,
        CancellationToken       ct)
    {
        var result = await service.CreateTagAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> DeleteTagAsync(
        DeleteReceiptTagRequest request,
        IReceiptService         service,
        CancellationToken       ct)
    {
        var result = await service.DeleteTagAsync(request, ct);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> SetReceiptTagsAsync(
        SetReceiptTagsRequest request,
        IReceiptService       service,
        CancellationToken     ct)
    {
        var result = await service.SetReceiptTagsAsync(request, ct);
        return result.ToHttpResponse();
    }
}
