using Application.Common.Constants;
using Application.Features.Receipt.DbModels;
using Application.Features.Receipt.DTOs;
using Application.Features.Receipt.Jobs;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Shared.Constants;
using Shared.Enums.Receipts;
using Shared.Enums.System;
using Shared.Responses;
using Shared.Results;
using System.Security.Cryptography;

namespace Application.Features.Receipt.Services;

internal sealed class ReceiptService(
    IReceiptRepository     receiptRepository,
    IFileService           fileService,
    IStorageUtility        storageUtility,
    IFileLinkService       fileLinkService,
    IBackgroundJobService  backgroundJobService,
    IUserContext           userContext,
    IMessageProvider       messageProvider) : IReceiptService
{
    // ─────────────────────────────────────────────────────────────────────────
    // UPLOAD
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<UploadReceiptResponse>> UploadAsync(
        UploadReceiptRequest request,
        CancellationToken    ct)
    {
        // Buffer file into memory (size is pre-validated by ValidationFilter)
        await using var ms = new MemoryStream();
        await request.File.CopyToAsync(ms, ct);
        ms.Position = 0;

        // Validate MIME type via magic bytes
        if (!IsValidMimeType(ms, request.File.FileName))
        {
            return ServiceResultFactory.Failure<UploadReceiptResponse>(
                InternalResponseCodes.BadRequest,
                await messageProvider.GetMessagesAsync(MessageKeys.Receipt.CorruptOrUnsupportedFile, ct));
        }
        ms.Position = 0;

        // Compute SHA-256 hash for duplicate detection
        var fileHash = ComputeHash(ms);
        ms.Position = 0;

        var ext           = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        var storedFileName = $"{Guid.NewGuid()}{ext}";
        var fileKey       = storageUtility.BuildFileKey(FolderPaths.Receipts, storedFileName);

        var dbModel = new UploadReceiptDbModel
        {
            UserId           = userContext.UserId,
            WorkspaceId      = userContext.WorkspaceId,
            OriginalFileName = SanitizeFileName(request.File.FileName),
            StoredFileName   = storedFileName,
            FileExtension    = ext,
            FileSizeBytes    = request.File.Length,
            ContentType      = request.File.ContentType,
            FileHash         = fileHash,
            Title            = request.Title?.Trim(),
            Description      = request.Description?.Trim(),
            ReceiptDate      = ParseDate(request.ReceiptDate),
            MerchantName     = request.MerchantName?.Trim(),
            Amount           = request.Amount,
            CurrencyCode     = request.CurrencyCode?.Trim().ToUpperInvariant(),
            Notes            = request.Notes?.Trim()
        };

        var dbResult = await receiptRepository.UploadAsync(dbModel, ct);

        if (dbResult.IsDuplicate)
        {
            return ServiceResultFactory.Failure<UploadReceiptResponse>(
                InternalResponseCodes.Conflict,
                await messageProvider.GetMessagesAsync(MessageKeys.Receipt.DuplicateFile, ct));
        }

        // Upload file to storage
        await fileService.UploadAsync(ms, fileKey, request.File.ContentType, ct);

        // Assign initial tags if provided
        if (!string.IsNullOrWhiteSpace(request.TagIds))
        {
            await receiptRepository.SetTagsAsync(userContext.UserId, userContext.WorkspaceId, dbResult.ReceiptId, request.TagIds, ct);
        }

        // Enqueue OCR if this is a processable type
        if (IsOcrEligible(ext, request.File.ContentType))
        {
            await backgroundJobService.EnqueueAsync(
                JobTypes.ProcessReceiptOcr,
                new ProcessReceiptOcrPayload(dbResult.ReceiptId, storedFileName, ext, request.File.ContentType),
                priority: 2,  // Normal
                ct: ct);
        }
        else
        {
            // Mark as Skipped so it won't be picked up by the OCR batch processor
            await receiptRepository.SetProcessingStatusAsync(
                dbResult.ReceiptId, (byte)ReceiptProcessingStatus.Skipped, ct);
        }

        var fileUrl = fileLinkService.CreateSignedFileUrl(
            userContext.RequestBaseUrl, FolderPaths.Receipts, storedFileName);

        return ServiceResultFactory.Success(
            new UploadReceiptResponse(
                ReceiptId:          dbResult.ReceiptId,
                OriginalFileName:   SanitizeFileName(request.File.FileName),
                FileUrl:            fileUrl,
                ThumbnailUrl:       null,
                StatusId:           (byte)ReceiptStatus.Active,
                ProcessingStatusId: IsOcrEligible(ext, request.File.ContentType)
                                        ? (byte)ReceiptProcessingStatus.Pending
                                        : (byte)ReceiptProcessingStatus.Skipped,
                OcrProcessed:       false,
                CreatedOnUtc:       DateTime.UtcNow),
            InternalResponseCodes.Created,
            await messageProvider.GetMessagesAsync(MessageKeys.Receipt.Uploaded, ct));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET BY ID
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<ReceiptDetailResponse>> GetByIdAsync(
        GetReceiptRequest request,
        CancellationToken ct)
    {
        var dbResult = await receiptRepository.GetByIdAsync(userContext.UserId, userContext.WorkspaceId, request.ReceiptId, ct);
        if (dbResult.Receipt is null)
        {
            return ServiceResultFactory.Failure<ReceiptDetailResponse>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Receipt.NotFound, ct));
        }

        var receipt = dbResult.Receipt;
        var tags    = dbResult.Tags;

        var fileUrl = fileLinkService.CreateSignedFileUrl(
            userContext.RequestBaseUrl, FolderPaths.Receipts, receipt.StoredFileName);

        string? thumbnailUrl = string.IsNullOrEmpty(receipt.ThumbnailFileName)
            ? null
            : fileLinkService.CreateSignedFileUrl(
                userContext.RequestBaseUrl, FolderPaths.Receipts, receipt.ThumbnailFileName);

        var response = new ReceiptDetailResponse(
            ReceiptId:          receipt.ReceiptId,
            OriginalFileName:   receipt.OriginalFileName,
            FileUrl:            fileUrl,
            ThumbnailUrl:       thumbnailUrl,
            FileSizeBytes:      receipt.FileSizeBytes,
            ContentType:        receipt.ContentType,
            Title:              receipt.Title,
            Description:        receipt.Description,
            ReceiptDate:        receipt.ReceiptDate?.ToString("yyyy-MM-dd"),
            MerchantName:       receipt.MerchantName,
            Amount:             receipt.Amount,
            CurrencyCode:       receipt.CurrencyCode,
            Notes:              receipt.Notes,
            StatusId:           receipt.StatusId,
            ProcessingStatusId: receipt.ProcessingStatusId,
            OcrProcessed:       receipt.OcrProcessed,
            TransactionId:      receipt.TransactionId,
            Tags:               tags.Select(t => new ReceiptTagResponse(t.TagId, t.Name, t.ColorHex)).ToList(),
            CreatedOnUtc:       receipt.CreatedOnUtc,
            UpdatedOnUtc:       receipt.UpdatedOnUtc);

        return ServiceResultFactory.Success(
            response,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Receipt.GetSuccess, ct));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SEARCH
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<PagedResponse<ReceiptSummaryResponse>>> SearchAsync(
        SearchReceiptsRequest request,
        CancellationToken     ct)
    {
        var dbModel = new SearchReceiptsDbModel
        {
            UserId     = userContext.UserId,
            WorkspaceId = userContext.WorkspaceId,
            Keyword    = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim(),
            StatusId   = request.StatusId,
            DateFrom   = ParseDate(request.DateFrom),
            DateTo     = ParseDate(request.DateTo),
            AmountMin  = request.AmountMin,
            AmountMax  = request.AmountMax,
            TagId      = request.TagId,
            PageNumber = request.PageNumber,
            PageSize   = request.PageSize
        };

        var dbResult = await receiptRepository.SearchAsync(dbModel, ct);

        var baseUrl = userContext.RequestBaseUrl;
        var items = dbResult.Items
            .Select(r => MapToSummary(r, baseUrl))
            .ToList();

        var response = new PagedResponse<ReceiptSummaryResponse>(
            Items:      items,
            TotalCount: dbResult.TotalCount,
            PageNumber: request.PageNumber,
            PageSize:   request.PageSize);

        return ServiceResultFactory.Success(
            response,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Receipt.SearchLoaded, ct));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UPDATE
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<object?>> UpdateAsync(
        UpdateReceiptRequest request,
        CancellationToken    ct)
    {
        var dbModel = new UpdateReceiptDbModel
        {
            UserId       = userContext.UserId,
            WorkspaceId  = userContext.WorkspaceId,
            ReceiptId    = request.ReceiptId,
            Title        = request.Title?.Trim(),
            Description  = request.Description?.Trim(),
            ReceiptDate  = ParseDate(request.ReceiptDate),
            MerchantName = request.MerchantName?.Trim(),
            Amount       = request.Amount,
            CurrencyCode = request.CurrencyCode?.Trim().ToUpperInvariant(),
            Notes        = request.Notes?.Trim()
        };

        var rowsAffected = await receiptRepository.UpdateAsync(dbModel, ct);
        if (rowsAffected == 0)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Receipt.NotFound, ct));
        }

        // Update tags if TagIds was provided (null = don't change, non-null string = update)
        if (request.TagIds is not null)
        {
            await receiptRepository.SetTagsAsync(userContext.UserId, userContext.WorkspaceId, request.ReceiptId, request.TagIds, ct);
        }

        return ServiceResultFactory.Success<object?>(
            null,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Receipt.Updated, ct));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DELETE
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<object?>> DeleteAsync(
        ReceiptActionRequest request,
        CancellationToken    ct)
    {
        var dbResult = await receiptRepository.DeleteAsync(userContext.UserId, userContext.WorkspaceId, request.ReceiptId, ct);
        if (dbResult.RowsAffected == 0)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Receipt.NotFound, ct));
        }

        // Physical delete files from storage
        if (!string.IsNullOrEmpty(dbResult.StoredFileName))
        {
            var fileKey = storageUtility.BuildFileKey(FolderPaths.Receipts, dbResult.StoredFileName);
            await fileService.DeleteAsync(fileKey, ct);
        }

        if (!string.IsNullOrEmpty(dbResult.ThumbnailFileName))
        {
            var thumbKey = storageUtility.BuildFileKey(FolderPaths.Receipts, dbResult.ThumbnailFileName);
            await fileService.DeleteAsync(thumbKey, ct);
        }

        return ServiceResultFactory.Success<object?>(
            null,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Receipt.Deleted, ct));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ARCHIVE
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<object?>> ArchiveAsync(
        ReceiptActionRequest request,
        CancellationToken    ct)
    {
        var rowsAffected = await receiptRepository.ArchiveAsync(userContext.UserId, userContext.WorkspaceId, request.ReceiptId, ct);
        if (rowsAffected == 0)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.BadRequest,
                await messageProvider.GetMessagesAsync(MessageKeys.Receipt.CannotArchiveNonActive, ct));
        }

        return ServiceResultFactory.Success<object?>(
            null,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Receipt.Archived, ct));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RESTORE
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<object?>> RestoreAsync(
        ReceiptActionRequest request,
        CancellationToken    ct)
    {
        var rowsAffected = await receiptRepository.RestoreAsync(userContext.UserId, userContext.WorkspaceId, request.ReceiptId, ct);
        if (rowsAffected == 0)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.BadRequest,
                await messageProvider.GetMessagesAsync(MessageKeys.Receipt.CannotRestoreNonArchived, ct));
        }

        return ServiceResultFactory.Success<object?>(
            null,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Receipt.Restored, ct));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ASSIGN TRANSACTION
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<object?>> AssignTransactionAsync(
        AssignTransactionRequest request,
        CancellationToken        ct)
    {
        var rowsAffected = await receiptRepository.AssignTransactionAsync(
            userContext.UserId, userContext.WorkspaceId, request.ReceiptId, request.TransactionId, ct);

        if (rowsAffected == -1)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Receipt.TransactionNotFound, ct));
        }

        if (rowsAffected == 0)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Receipt.NotFound, ct));
        }

        var messageKey = request.TransactionId.HasValue
            ? MessageKeys.Receipt.TransactionAssigned
            : MessageKeys.Receipt.TransactionUnlinked;

        return ServiceResultFactory.Success<object?>(
            null,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(messageKey, ct));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DASHBOARD
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<ReceiptDashboardResponse>> GetDashboardAsync(CancellationToken ct)
    {
        var dbResult = await receiptRepository.GetDashboardAsync(userContext.UserId, userContext.WorkspaceId, ct);
        var baseUrl  = userContext.RequestBaseUrl;

        var summary = new ReceiptDashboardSummaryResponse(
            TotalCount:               dbResult.Summary.TotalCount,
            ActiveCount:              dbResult.Summary.ActiveCount,
            ArchivedCount:            dbResult.Summary.ArchivedCount,
            OcrProcessedCount:        dbResult.Summary.OcrProcessedCount,
            OcrFailedCount:           dbResult.Summary.OcrFailedCount,
            TotalSizeBytes:           dbResult.Summary.TotalSizeBytes,
            LinkedToTransactionCount: dbResult.Summary.LinkedToTransactionCount);

        var recent = dbResult.Recent
            .Select(r => MapToSummary(r, baseUrl))
            .ToList();

        return ServiceResultFactory.Success(
            new ReceiptDashboardResponse(summary, recent),
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Receipt.DashboardLoaded, ct));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TAGS
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<IReadOnlyList<ReceiptTagListResponse>>> GetTagsAsync(CancellationToken ct)
    {
        var tags = await receiptRepository.GetTagListAsync(userContext.UserId, userContext.WorkspaceId, ct);

        var response = tags
            .Select(t => new ReceiptTagListResponse(t.TagId, t.Name, t.ColorHex, t.UsageCount, t.CreatedOnUtc))
            .ToList();

        return ServiceResultFactory.Success<IReadOnlyList<ReceiptTagListResponse>>(
            response,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Receipt.TagsLoaded, ct));
    }

    public async Task<ServiceResult<ReceiptTagListResponse>> CreateTagAsync(
        CreateReceiptTagRequest request,
        CancellationToken       ct)
    {
        var dbModel = new CreateReceiptTagDbModel
        {
            UserId   = userContext.UserId,
            WorkspaceId = userContext.WorkspaceId,
            Name     = request.Name.Trim(),
            ColorHex = request.ColorHex?.Trim()
        };

        var dbResult = await receiptRepository.CreateTagAsync(dbModel, ct);

        if (dbResult.IsDuplicate)
        {
            return ServiceResultFactory.Failure<ReceiptTagListResponse>(
                InternalResponseCodes.Conflict,
                await messageProvider.GetMessagesAsync(MessageKeys.Receipt.TagDuplicate, ct));
        }

        return ServiceResultFactory.Success(
            new ReceiptTagListResponse(dbResult.TagId, request.Name.Trim(), request.ColorHex?.Trim(), 0, DateTime.UtcNow),
            InternalResponseCodes.Created,
            await messageProvider.GetMessagesAsync(MessageKeys.Receipt.TagCreated, ct));
    }

    public async Task<ServiceResult<object?>> DeleteTagAsync(
        DeleteReceiptTagRequest request,
        CancellationToken       ct)
    {
        var rowsAffected = await receiptRepository.DeleteTagAsync(userContext.UserId, userContext.WorkspaceId, request.TagId, ct);
        if (rowsAffected == 0)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Receipt.TagNotFound, ct));
        }

        return ServiceResultFactory.Success<object?>(
            null,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Receipt.TagDeleted, ct));
    }

    public async Task<ServiceResult<object?>> SetReceiptTagsAsync(
        SetReceiptTagsRequest request,
        CancellationToken     ct)
    {
        // Verify receipt exists first
        var dbResult = await receiptRepository.GetByIdAsync(userContext.UserId, userContext.WorkspaceId, request.ReceiptId, ct);
        if (dbResult.Receipt is null)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Receipt.NotFound, ct));
        }

        await receiptRepository.SetTagsAsync(userContext.UserId, userContext.WorkspaceId, request.ReceiptId, request.TagIds, ct);

        return ServiceResultFactory.Success<object?>(
            null,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Receipt.TagsUpdated, ct));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DOWNLOAD
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<DownloadReceiptResponse>> DownloadAsync(
        DownloadReceiptRequest request,
        CancellationToken      ct)
    {
        var dbResult = await receiptRepository.GetByIdAsync(userContext.UserId, userContext.WorkspaceId, request.ReceiptId, ct);
        if (dbResult.Receipt is null)
        {
            return ServiceResultFactory.Failure<DownloadReceiptResponse>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Receipt.NotFound, ct));
        }

        var receipt = dbResult.Receipt;
        var fileKey = storageUtility.BuildFileKey(FolderPaths.Receipts, receipt.StoredFileName);
        var stream  = await fileService.DownloadAsync(fileKey, ct);

        return ServiceResultFactory.Success(
            new DownloadReceiptResponse(
                Stream:        stream,
                ContentType:   receipt.ContentType,
                FileName:      receipt.OriginalFileName,
                FileSizeBytes: receipt.FileSizeBytes),
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Receipt.DownloadReady, ct));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PRIVATE HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    private static string ComputeHash(Stream stream)
    {
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // Validates file magic bytes against declared extension to prevent MIME spoofing.
    private static bool IsValidMimeType(Stream stream, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        Span<byte> header = stackalloc byte[12];
        var read = stream.Read(header);
        if (read == 0) return false;

        return ext switch
        {
            ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png"            => header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
            ".gif"            => header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38,
            ".pdf"            => header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46,
            ".webp"           => read >= 12 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                                             && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50,
            // HEIC uses ISO BMFF container — varied magic bytes; skip deep validation
            ".heic"           => read >= 4,
            _                 => false
        };
    }

    private static bool IsOcrEligible(string ext, string contentType) =>
        ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".pdf";

    private static string SanitizeFileName(string fileName)
    {
        // Strip path separators that could indicate a path traversal attempt
        var name = Path.GetFileName(fileName);
        return string.IsNullOrWhiteSpace(name) ? "receipt" : name;
    }

    private static DateOnly? ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;
        return DateOnly.TryParse(dateStr, out var date) ? date : null;
    }

    private ReceiptSummaryResponse MapToSummary(ReceiptSummaryDbResult r, string baseUrl)
    {
        var fileUrl = fileLinkService.CreateSignedFileUrl(baseUrl, FolderPaths.Receipts, r.StoredFileName);

        string? thumbnailUrl = string.IsNullOrEmpty(r.ThumbnailFileName)
            ? null
            : fileLinkService.CreateSignedFileUrl(baseUrl, FolderPaths.Receipts, r.ThumbnailFileName);

        return new ReceiptSummaryResponse(
            ReceiptId:          r.ReceiptId,
            OriginalFileName:   r.OriginalFileName,
            FileUrl:            fileUrl,
            ThumbnailUrl:       thumbnailUrl,
            Title:              r.Title,
            ReceiptDate:        r.ReceiptDate?.ToString("yyyy-MM-dd"),
            MerchantName:       r.MerchantName,
            Amount:             r.Amount,
            CurrencyCode:       r.CurrencyCode,
            StatusId:           r.StatusId,
            ProcessingStatusId: r.ProcessingStatusId,
            OcrProcessed:       r.OcrProcessed,
            TransactionId:      r.TransactionId,
            TagCount:           r.TagCount,
            CreatedOnUtc:       r.CreatedOnUtc,
            UpdatedOnUtc:       r.UpdatedOnUtc);
    }
}
