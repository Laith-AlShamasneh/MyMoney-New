namespace Application.Features.Receipt.Jobs;

public record ProcessReceiptOcrPayload(
    long   ReceiptId,
    string StoredFileName,
    string FileExtension,
    string ContentType
);
