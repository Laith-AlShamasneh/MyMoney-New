using Application.Interfaces.Services;

namespace Infrastructure.Services.Ocr;

// Placeholder OCR provider — returns null (no extraction).
// Replace with a real provider (Azure AI Vision, OpenAI, Tesseract) by implementing IOcrProvider.
internal sealed class LocalOcrProvider : IOcrProvider
{
    public string ProviderName => "Local";

    public bool CanProcess(string fileExtension, string contentType) =>
        fileExtension is ".jpg" or ".jpeg" or ".png" or ".webp" or ".pdf";

    public Task<OcrExtractionResult?> ExtractAsync(
        Stream            fileStream,
        string            fileExtension,
        CancellationToken ct)
    {
        // No local OCR engine is installed; return null to record a skipped result.
        return Task.FromResult<OcrExtractionResult?>(null);
    }
}
