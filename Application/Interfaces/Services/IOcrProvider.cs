namespace Application.Interfaces.Services;

public sealed record OcrExtractionResult(
    string?  RawText,
    string?  MerchantName,
    decimal? TotalAmount,
    DateOnly? ReceiptDate,
    decimal? Confidence,
    string   ProviderName
);

public interface IOcrProvider
{
    bool   CanProcess(string fileExtension, string contentType);
    string ProviderName { get; }
    Task<OcrExtractionResult?> ExtractAsync(Stream fileStream, string fileExtension, CancellationToken ct);
}
