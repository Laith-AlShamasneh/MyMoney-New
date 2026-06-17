namespace Shared.Constants;

public static class MessageKeys
{
    public static class Common
    {
        public const string Success             = "Common.Success";
        public const string Created             = "Common.Created";
        public const string Updated             = "Common.Updated";
        public const string Deleted             = "Common.Deleted";
        public const string NotFound            = "Common.NotFound";
        public const string BadRequest          = "Common.BadRequest";
        public const string ValidationError     = "Common.ValidationError";
        public const string Unauthorized        = "Common.Unauthorized";
        public const string Forbidden           = "Common.Forbidden";
        public const string Conflict            = "Common.Conflict";
        public const string InternalServerError = "Common.InternalServerError";
    }

    public static class Authentication
    {
        // Validation
        public const string FirstNameRequired           = "Authentication.FirstNameRequired";
        public const string LastNameRequired            = "Authentication.LastNameRequired";
        public const string EmailRequired               = "Authentication.EmailRequired";
        public const string InvalidEmail                = "Authentication.InvalidEmail";
        public const string PasswordRequired            = "Authentication.PasswordRequired";
        public const string PasswordTooShort            = "Authentication.PasswordTooShort";
        public const string PasswordUppercaseRequired   = "Authentication.PasswordUppercaseRequired";
        public const string PasswordLowercaseRequired   = "Authentication.PasswordLowercaseRequired";
        public const string PasswordDigitRequired       = "Authentication.PasswordDigitRequired";
        public const string PasswordSpecialRequired     = "Authentication.PasswordSpecialRequired";
        public const string ConfirmPasswordRequired     = "Authentication.ConfirmPasswordRequired";
        public const string PasswordMismatch            = "Authentication.PasswordMismatch";
        public const string RefreshTokenRequired        = "Authentication.RefreshTokenRequired";
        public const string ResetTokenRequired          = "Authentication.ResetTokenRequired";
        public const string NewPasswordRequired         = "Authentication.NewPasswordRequired";

        // Validation (Register-specific)
        public const string FirstNameTooLong              = "Authentication.FirstNameTooLong";
        public const string LastNameTooLong               = "Authentication.LastNameTooLong";
        public const string DisplayNameRequired           = "Authentication.DisplayNameRequired";
        public const string DisplayNameTooLong            = "Authentication.DisplayNameTooLong";
        public const string InvalidDateOfBirth            = "Authentication.InvalidDateOfBirth";
        public const string InvalidProfileImageFormat     = "Authentication.InvalidProfileImageFormat";
        public const string ProfileImageTooLarge          = "Authentication.ProfileImageTooLarge";

        // Business
        public const string RegistrationFailed       = "Authentication.RegistrationFailed";
        public const string EmailAlreadyInUse        = "Authentication.EmailAlreadyInUse";
        public const string InvalidCredentials      = "Authentication.InvalidCredentials";
        public const string AccountLocked           = "Authentication.AccountLocked";
        public const string AccountNotActive        = "Authentication.AccountNotActive";
        public const string EmailNotConfirmed       = "Authentication.EmailNotConfirmed";
        public const string InvalidToken            = "Authentication.InvalidToken";
        public const string TokenExpired            = "Authentication.TokenExpired";
        public const string TokenRefreshed          = "Authentication.TokenRefreshed";
        public const string TokenRevoked            = "Authentication.TokenRevoked";
        public const string ResetEmailSent          = "Authentication.ResetEmailSent";
        public const string InvalidResetToken       = "Authentication.InvalidResetToken";
        public const string PasswordResetSuccess    = "Authentication.PasswordResetSuccess";
        public const string UserLoginSuccess        = "Authentication.UserLoginSuccess";
        public const string UserRegisteredSuccess   = "Authentication.UserRegisteredSuccess";

        // Change password
        public const string CurrentPasswordRequired    = "Authentication.CurrentPasswordRequired";
        public const string CurrentPasswordIncorrect   = "Authentication.CurrentPasswordIncorrect";
        public const string NewPasswordSameAsCurrent   = "Authentication.NewPasswordSameAsCurrent";
        public const string PasswordChanged            = "Authentication.PasswordChanged";

        // Email confirmation
        public const string ConfirmationTokenRequired  = "Authentication.ConfirmationTokenRequired";
        public const string EmailConfirmed             = "Authentication.EmailConfirmed";
        public const string EmailAlreadyConfirmed      = "Authentication.EmailAlreadyConfirmed";
        public const string ConfirmationEmailSent      = "Authentication.ConfirmationEmailSent";

        // Password reset
        public const string ResetTokenValid            = "Authentication.ResetTokenValid";

        // Logout
        public const string LogoutSuccess              = "Authentication.LogoutSuccess";
    }

    public static class Transaction
    {
        // Validation
        public const string AmountRequired          = "Transaction.AmountRequired";
        public const string AmountMustBePositive    = "Transaction.AmountMustBePositive";
        public const string CategoryRequired        = "Transaction.CategoryRequired";
        public const string InvalidCategory         = "Transaction.InvalidCategory";
        public const string DateRequired            = "Transaction.DateRequired";
        public const string DateCannotBeFuture      = "Transaction.DateCannotBeFuture";
        public const string DescriptionTooLong      = "Transaction.DescriptionTooLong";
        public const string NotesTooLong            = "Transaction.NotesTooLong";
        public const string InvalidTransactionType  = "Transaction.InvalidTransactionType";
        public const string PageNumberInvalid       = "Transaction.PageNumberInvalid";
        public const string PageSizeInvalid         = "Transaction.PageSizeInvalid";
        public const string InvalidSortDirection    = "Transaction.InvalidSortDirection";
        public const string AmountRangeInvalid      = "Transaction.AmountRangeInvalid";
        public const string DateRangeInvalid        = "Transaction.DateRangeInvalid";
        public const string InvalidTransactionId    = "Transaction.InvalidTransactionId";

        // Business
        public const string NotFound         = "Transaction.NotFound";
        public const string Created          = "Transaction.Created";
        public const string Updated          = "Transaction.Updated";
        public const string Deleted          = "Transaction.Deleted";
        public const string SearchSuccess    = "Transaction.SearchSuccess";
        public const string AnalyticsLoaded  = "Transaction.AnalyticsLoaded";
    }

    public static class Category
    {
        public const string NotFound            = "Category.NotFound";
        public const string LoadedSuccessfully  = "Category.LoadedSuccessfully";
    }

    public static class Dashboard
    {
        public const string LoadedSuccessfully = "Dashboard.LoadedSuccessfully";
    }

    public static class Profile
    {
        // Validation
        public const string FirstNameRequired           = "Profile.FirstNameRequired";
        public const string FirstNameTooLong            = "Profile.FirstNameTooLong";
        public const string LastNameRequired            = "Profile.LastNameRequired";
        public const string LastNameTooLong             = "Profile.LastNameTooLong";
        public const string DisplayNameTooLong          = "Profile.DisplayNameTooLong";
        public const string InvalidGender               = "Profile.InvalidGender";
        public const string CurrentPasswordRequired     = "Profile.CurrentPasswordRequired";
        public const string NewPasswordRequired         = "Profile.NewPasswordRequired";
        public const string NewPasswordTooShort         = "Profile.NewPasswordTooShort";
        public const string NewPasswordUppercase        = "Profile.NewPasswordUppercase";
        public const string NewPasswordLowercase        = "Profile.NewPasswordLowercase";
        public const string NewPasswordDigit            = "Profile.NewPasswordDigit";
        public const string NewPasswordSpecial          = "Profile.NewPasswordSpecial";
        public const string ConfirmNewPasswordRequired  = "Profile.ConfirmNewPasswordRequired";
        public const string NewPasswordMismatch         = "Profile.NewPasswordMismatch";
        public const string InvalidProfilePictureFormat = "Profile.InvalidProfilePictureFormat";
        public const string ProfilePictureTooLarge      = "Profile.ProfilePictureTooLarge";

        // Email change validation
        public const string NewEmailRequired            = "Profile.NewEmailRequired";
        public const string NewEmailInvalid             = "Profile.NewEmailInvalid";
        public const string NewEmailTooLong             = "Profile.NewEmailTooLong";
        public const string EmailChangeTokenRequired    = "Profile.EmailChangeTokenRequired";

        // Business
        public const string NotFound                   = "Profile.NotFound";
        public const string Updated                    = "Profile.Updated";
        public const string PasswordChanged            = "Profile.PasswordChanged";
        public const string CurrentPasswordIncorrect   = "Profile.CurrentPasswordIncorrect";
        public const string NewPasswordSameAsCurrent   = "Profile.NewPasswordSameAsCurrent";
        public const string ProfilePictureUpdated      = "Profile.ProfilePictureUpdated";
        public const string ProfilePictureDeleted      = "Profile.ProfilePictureDeleted";
        public const string GetProfileSuccess          = "Profile.GetProfileSuccess";

        // Email change business
        public const string EmailChangeRequested       = "Profile.EmailChangeRequested";
        public const string EmailChangeConfirmed       = "Profile.EmailChangeConfirmed";
        public const string EmailChangeCancelled       = "Profile.EmailChangeCancelled";
        public const string NoPendingEmailChange       = "Profile.NoPendingEmailChange";
        public const string EmailChangeTokenExpired    = "Profile.EmailChangeTokenExpired";
        public const string EmailChangeInvalidToken    = "Profile.EmailChangeInvalidToken";
        public const string EmailAlreadyInUse          = "Profile.EmailAlreadyInUse";
        public const string EmailSameAsCurrent         = "Profile.EmailSameAsCurrent";

        // Session management business
        public const string GetSessionsSuccess         = "Profile.GetSessionsSuccess";
        public const string SessionRevoked             = "Profile.SessionRevoked";
        public const string AllOtherSessionsRevoked    = "Profile.AllOtherSessionsRevoked";
        public const string SessionNotFound            = "Profile.SessionNotFound";
    }

    public static class Notifications
    {
        // Service responses
        public const string ListLoaded         = "Notifications.ListLoaded";
        public const string UnreadCountLoaded  = "Notifications.UnreadCountLoaded";
        public const string MarkedAsRead       = "Notifications.MarkedAsRead";
        public const string AllMarkedAsRead    = "Notifications.AllMarkedAsRead";
        public const string Archived           = "Notifications.Archived";
        public const string Dismissed          = "Notifications.Dismissed";
        public const string Deleted            = "Notifications.Deleted";
        public const string NotFound           = "Notifications.NotFound";
        public const string PreferencesLoaded  = "Notifications.PreferencesLoaded";
        public const string PreferencesUpdated = "Notifications.PreferencesUpdated";

        // Validation
        public const string InvalidNotificationId = "Notifications.InvalidNotificationId";
        public const string InvalidPageSize       = "Notifications.InvalidPageSize";
        public const string InvalidPageNumber     = "Notifications.InvalidPageNumber";
    }

    public static class FinancialIntelligence
    {
        // Insights
        public const string InsightsLoaded             = "FinancialIntelligence.InsightsLoaded";
        public const string InsightMarkedRead          = "FinancialIntelligence.InsightMarkedRead";
        public const string AllInsightsMarkedRead      = "FinancialIntelligence.AllInsightsMarkedRead";
        public const string InsightNotFound            = "FinancialIntelligence.InsightNotFound";

        // Patterns
        public const string PatternsLoaded             = "FinancialIntelligence.PatternsLoaded";

        // Recommendations
        public const string RecommendationsLoaded      = "FinancialIntelligence.RecommendationsLoaded";
        public const string RecommendationApplied      = "FinancialIntelligence.RecommendationApplied";
        public const string RecommendationDismissed    = "FinancialIntelligence.RecommendationDismissed";
        public const string RecommendationNotFound     = "FinancialIntelligence.RecommendationNotFound";

        // Dashboard
        public const string DashboardLoaded            = "FinancialIntelligence.DashboardLoaded";

        // Snapshot
        public const string SnapshotGenerated          = "FinancialIntelligence.SnapshotGenerated";

        // Validation
        public const string InvalidPageNumber          = "FinancialIntelligence.InvalidPageNumber";
        public const string InvalidPageSize            = "FinancialIntelligence.InvalidPageSize";
        public const string InvalidInsightId           = "FinancialIntelligence.InvalidInsightId";
        public const string InvalidRecommendationId    = "FinancialIntelligence.InvalidRecommendationId";
    }

    public static class BackgroundJobs
    {
        public const string JobEnqueueFailed = "BackgroundJobs.JobEnqueueFailed";
    }

    public static class Reports
    {
        // Validation
        public const string ReportTypeRequired    = "Reports.ReportTypeRequired";
        public const string InvalidReportType     = "Reports.InvalidReportType";
        public const string LanguageRequired      = "Reports.LanguageRequired";
        public const string InvalidLanguage       = "Reports.InvalidLanguage";
        public const string DateFromRequired      = "Reports.DateFromRequired";
        public const string DateToRequired        = "Reports.DateToRequired";
        public const string InvalidDateRange      = "Reports.InvalidDateRange";
        public const string DateRangeTooLarge     = "Reports.DateRangeTooLarge";

        // Business
        public const string TypesLoaded          = "Reports.TypesLoaded";
        public const string Generated            = "Reports.Generated";
        public const string ListLoaded           = "Reports.ListLoaded";
        public const string NotFound             = "Reports.NotFound";
        public const string NotReady             = "Reports.NotReady";
        public const string Deleted              = "Reports.Deleted";
        public const string DownloadReady        = "Reports.DownloadReady";
    }
}
