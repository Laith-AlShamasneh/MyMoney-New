namespace Shared.Enums.Receipts;

public enum ReceiptProcessingStatus : byte
{
    Pending    = 1,
    Processing = 2,
    Completed  = 3,
    Failed     = 4,
    Skipped    = 5
}
