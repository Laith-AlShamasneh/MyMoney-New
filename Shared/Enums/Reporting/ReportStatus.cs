namespace Shared.Enums.Reporting;

public enum ReportStatus : byte
{
    Pending    = 1,
    Processing = 2,
    Completed  = 3,
    Failed     = 4,
    Expired    = 5
}
