namespace Application.Common.Constants;

/// <summary>
/// Template codes that identify what kind of notification to create.
/// Each code maps to a row in NotificationTemplates and its translations.
/// </summary>
public static class NotificationCodes
{
    // ── Security ─────────────────────────────────────────────────────────────
    public const string Welcome         = "Welcome";
    public const string PasswordChanged = "PasswordChanged";
    public const string EmailChanged    = "EmailChanged";
    public const string SessionRevoked  = "SessionRevoked";

    // ── Financial ─────────────────────────────────────────────────────────────
    public const string LargeTransaction   = "LargeTransaction";
    public const string BudgetExceeded     = "BudgetExceeded";
    public const string BudgetNearingLimit = "BudgetNearingLimit";

    // ── Reports ───────────────────────────────────────────────────────────────
    public const string ReportReady  = "ReportReady";
    public const string ReportFailed = "ReportFailed";

    // ── Profile ───────────────────────────────────────────────────────────────
    public const string ProfileUpdated        = "ProfileUpdated";
    public const string ProfilePictureChanged = "ProfilePictureChanged";

    // ── System ────────────────────────────────────────────────────────────────
    public const string SystemAnnouncement = "SystemAnnouncement";
    public const string MaintenanceNotice  = "MaintenanceNotice";

    // ── Financial Intelligence ─────────────────────────────────────────────────
    public const string FILOverspendingAlert   = "FIL_OverspendingAlert";
    public const string FILSpendingSpike       = "FIL_SpendingSpike";
    public const string FILUnusualTransaction  = "FIL_UnusualTransaction";
    public const string FILHighExpenseRatio    = "FIL_HighExpenseRatio";
    public const string FILAchievement         = "FIL_Achievement";
    public const string FILMonthlySummary      = "FIL_MonthlySummary";
}
