namespace Application.Features.Receipt.DbModels;

// ── Upload ────────────────────────────────────────────────────────────────────

public class UploadReceiptDbModel
{
    public long    UserId           { get; set; }
    public long?   WorkspaceId      { get; set; }
    public string  OriginalFileName { get; set; } = null!;
    public string  StoredFileName   { get; set; } = null!;
    public string  FileExtension    { get; set; } = null!;
    public long    FileSizeBytes    { get; set; }
    public string  ContentType      { get; set; } = null!;
    public string  FileHash         { get; set; } = null!;
    public string? Title            { get; set; }
    public string? Description      { get; set; }
    public DateOnly? ReceiptDate    { get; set; }
    public string? MerchantName     { get; set; }
    public decimal? Amount          { get; set; }
    public string? CurrencyCode     { get; set; }
    public string? Notes            { get; set; }
}

public class UploadReceiptDbResult
{
    public long ReceiptId   { get; set; }
    public bool IsDuplicate { get; set; }
    public long DuplicateId { get; set; }
}

// ── GetById ───────────────────────────────────────────────────────────────────

public class ReceiptWithTagsDbResult
{
    public ReceiptDetailDbResult?              Receipt { get; set; }
    public IReadOnlyList<ReceiptTagDbResult> Tags    { get; set; } = [];
}

public class ReceiptDetailDbResult
{
    public long     ReceiptId         { get; set; }
    public long     UserId            { get; set; }
    public long?    TransactionId     { get; set; }
    public string   OriginalFileName  { get; set; } = null!;
    public string   StoredFileName    { get; set; } = null!;
    public string   FileExtension     { get; set; } = null!;
    public long     FileSizeBytes     { get; set; }
    public string   ContentType       { get; set; } = null!;
    public string?  Title             { get; set; }
    public string?  Description       { get; set; }
    public DateOnly? ReceiptDate      { get; set; }
    public string?  MerchantName      { get; set; }
    public decimal? Amount            { get; set; }
    public string?  CurrencyCode      { get; set; }
    public string?  Notes             { get; set; }
    public byte     StatusId          { get; set; }
    public byte     ProcessingStatusId { get; set; }
    public bool     OcrProcessed      { get; set; }
    public string?  ThumbnailFileName { get; set; }
    public DateTime CreatedOnUtc      { get; set; }
    public DateTime? UpdatedOnUtc     { get; set; }
}

public class ReceiptTagDbResult
{
    public int    TagId    { get; set; }
    public string Name     { get; set; } = null!;
    public string? ColorHex { get; set; }
}

// ── Search ────────────────────────────────────────────────────────────────────

public class SearchReceiptsDbModel
{
    public long    UserId      { get; set; }
    public long?   WorkspaceId { get; set; }
    public string? Keyword    { get; set; }
    public byte?   StatusId   { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo   { get; set; }
    public decimal? AmountMin { get; set; }
    public decimal? AmountMax { get; set; }
    public int?    TagId      { get; set; }
    public int     PageNumber { get; set; } = 1;
    public int     PageSize   { get; set; } = 20;
}

public class ReceiptSummaryDbResult
{
    public long     ReceiptId          { get; set; }
    public string   OriginalFileName   { get; set; } = null!;
    public string   StoredFileName     { get; set; } = null!;
    public string   FileExtension      { get; set; } = null!;
    public long     FileSizeBytes      { get; set; }
    public string   ContentType        { get; set; } = null!;
    public string?  Title              { get; set; }
    public DateOnly? ReceiptDate       { get; set; }
    public string?  MerchantName       { get; set; }
    public decimal? Amount             { get; set; }
    public string?  CurrencyCode       { get; set; }
    public byte     StatusId           { get; set; }
    public byte     ProcessingStatusId { get; set; }
    public bool     OcrProcessed       { get; set; }
    public string?  ThumbnailFileName  { get; set; }
    public long?    TransactionId      { get; set; }
    public int      TagCount           { get; set; }
    public DateTime CreatedOnUtc       { get; set; }
    public DateTime? UpdatedOnUtc      { get; set; }
}

public class SearchReceiptsDbResult
{
    public IReadOnlyList<ReceiptSummaryDbResult> Items      { get; set; } = [];
    public int                                   TotalCount { get; set; }
}

// ── Update ────────────────────────────────────────────────────────────────────

public class UpdateReceiptDbModel
{
    public long    UserId       { get; set; }
    public long?   WorkspaceId  { get; set; }
    public long    ReceiptId    { get; set; }
    public string? Title        { get; set; }
    public string? Description  { get; set; }
    public DateOnly? ReceiptDate { get; set; }
    public string? MerchantName { get; set; }
    public decimal? Amount      { get; set; }
    public string? CurrencyCode { get; set; }
    public string? Notes        { get; set; }
}

// ── Delete ────────────────────────────────────────────────────────────────────

public class DeleteReceiptDbResult
{
    public string? StoredFileName    { get; set; }
    public string? ThumbnailFileName { get; set; }
    public int     RowsAffected      { get; set; }
}

// ── Dashboard ─────────────────────────────────────────────────────────────────

public class ReceiptDashboardSummaryDbResult
{
    public int  TotalCount               { get; set; }
    public int  ActiveCount              { get; set; }
    public int  ArchivedCount            { get; set; }
    public int  OcrProcessedCount        { get; set; }
    public int  OcrFailedCount           { get; set; }
    public long TotalSizeBytes           { get; set; }
    public int  LinkedToTransactionCount { get; set; }
}

public class ReceiptDashboardDbResult
{
    public ReceiptDashboardSummaryDbResult         Summary { get; set; } = new();
    public IReadOnlyList<ReceiptSummaryDbResult> Recent  { get; set; } = [];
}

// ── Tags ──────────────────────────────────────────────────────────────────────

public class ReceiptTagListDbResult
{
    public int    TagId      { get; set; }
    public string Name       { get; set; } = null!;
    public string? ColorHex  { get; set; }
    public int    UsageCount { get; set; }
    public DateTime CreatedOnUtc { get; set; }
}

public class CreateReceiptTagDbModel
{
    public long   UserId      { get; set; }
    public long?  WorkspaceId { get; set; }
    public string Name     { get; set; } = null!;
    public string? ColorHex { get; set; }
}

public class CreateReceiptTagDbResult
{
    public int  TagId       { get; set; }
    public bool IsDuplicate { get; set; }
}

// ── Pending OCR ───────────────────────────────────────────────────────────────

public class PendingOcrReceiptDbResult
{
    public long   ReceiptId      { get; set; }
    public string StoredFileName { get; set; } = null!;
    public string FileExtension  { get; set; } = null!;
    public string ContentType    { get; set; } = null!;
}
