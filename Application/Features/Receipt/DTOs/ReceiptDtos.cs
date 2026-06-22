using Microsoft.AspNetCore.Http;

namespace Application.Features.Receipt.DTOs;

// ── Upload ────────────────────────────────────────────────────────────────────

public sealed record UploadReceiptRequest(
    IFormFile   File,
    string?     Title        = null,
    string?     Description  = null,
    string?     ReceiptDate  = null,
    string?     MerchantName = null,
    decimal?    Amount       = null,
    string?     CurrencyCode = null,
    string?     Notes        = null,
    string?     TagIds       = null  // JSON array e.g. "[1,2,3]"
);

public sealed record UploadReceiptResponse(
    long    ReceiptId,
    string  OriginalFileName,
    string  FileUrl,
    string? ThumbnailUrl,
    byte    StatusId,
    byte    ProcessingStatusId,
    bool    OcrProcessed,
    DateTime CreatedOnUtc
);

// ── Search ────────────────────────────────────────────────────────────────────

public sealed record SearchReceiptsRequest(
    string?  Keyword,
    byte?    StatusId,
    string?  DateFrom,
    string?  DateTo,
    decimal? AmountMin,
    decimal? AmountMax,
    int?     TagId,
    int      PageNumber = 1,
    int      PageSize   = 20
);

public sealed record ReceiptSummaryResponse(
    long     ReceiptId,
    string   OriginalFileName,
    string   FileUrl,
    string?  ThumbnailUrl,
    string?  Title,
    string?  ReceiptDate,
    string?  MerchantName,
    decimal? Amount,
    string?  CurrencyCode,
    byte     StatusId,
    byte     ProcessingStatusId,
    bool     OcrProcessed,
    long?    TransactionId,
    int      TagCount,
    DateTime CreatedOnUtc,
    DateTime? UpdatedOnUtc
);

// ── GetById ───────────────────────────────────────────────────────────────────

public sealed record GetReceiptRequest(long ReceiptId);

public sealed record ReceiptTagResponse(int TagId, string Name, string? ColorHex);

public sealed record ReceiptDetailResponse(
    long     ReceiptId,
    string   OriginalFileName,
    string   FileUrl,
    string?  ThumbnailUrl,
    long     FileSizeBytes,
    string   ContentType,
    string?  Title,
    string?  Description,
    string?  ReceiptDate,
    string?  MerchantName,
    decimal? Amount,
    string?  CurrencyCode,
    string?  Notes,
    byte     StatusId,
    byte     ProcessingStatusId,
    bool     OcrProcessed,
    long?    TransactionId,
    IReadOnlyList<ReceiptTagResponse> Tags,
    DateTime CreatedOnUtc,
    DateTime? UpdatedOnUtc
);

// ── Update ────────────────────────────────────────────────────────────────────

public sealed record UpdateReceiptRequest(
    long     ReceiptId,
    string?  Title,
    string?  Description,
    string?  ReceiptDate,
    string?  MerchantName,
    decimal? Amount,
    string?  CurrencyCode,
    string?  Notes,
    string?  TagIds  // JSON array e.g. "[1,2,3]", null = don't change, "[]" = clear all
);

// ── Delete / Archive / Restore ────────────────────────────────────────────────

public sealed record ReceiptActionRequest(long ReceiptId);

// ── Assign Transaction ────────────────────────────────────────────────────────

public sealed record AssignTransactionRequest(long ReceiptId, long? TransactionId);

// ── Dashboard ─────────────────────────────────────────────────────────────────

public sealed record ReceiptDashboardSummaryResponse(
    int  TotalCount,
    int  ActiveCount,
    int  ArchivedCount,
    int  OcrProcessedCount,
    int  OcrFailedCount,
    long TotalSizeBytes,
    int  LinkedToTransactionCount
);

public sealed record ReceiptDashboardResponse(
    ReceiptDashboardSummaryResponse           Summary,
    IReadOnlyList<ReceiptSummaryResponse> Recent
);

// ── Tags ──────────────────────────────────────────────────────────────────────

public sealed record ReceiptTagListResponse(
    int      TagId,
    string   Name,
    string?  ColorHex,
    int      UsageCount,
    DateTime CreatedOnUtc
);

public sealed record CreateReceiptTagRequest(string Name, string? ColorHex);

public sealed record DeleteReceiptTagRequest(int TagId);

public sealed record SetReceiptTagsRequest(long ReceiptId, string TagIds);  // JSON array

// ── Download ──────────────────────────────────────────────────────────────────

public sealed record DownloadReceiptRequest(long ReceiptId);

public sealed record DownloadReceiptResponse(
    Stream  Stream,
    string  ContentType,
    string  FileName,
    long    FileSizeBytes
);
