namespace Shared.Constants;

public static class MessageKeys
{
    public static class Common
    {
        public const string Success = "Common.Success";
        public const string Created = "Common.Created";
        public const string Updated = "Common.Updated";
        public const string Deleted = "Common.Deleted";
        public const string NotFound = "Common.NotFound";
        public const string BadRequest = "Common.BadRequest";
        public const string ValidationError = "Common.ValidationError";
        public const string Unauthorized = "Common.Unauthorized";
        public const string Forbidden = "Common.Forbidden";
        public const string Conflict = "Common.Conflict";
        public const string InternalServerError = "Common.InternalServerError";
    }

    public static class User
    {
        public const string UserNotFound = "User.UserNotFound";
        public const string UserCreatedSuccess = "User.UserCreatedSuccess";
    }

    public static class Authentication
    {
        public const string EmailRequired = "Authentication.EmailRequired";
        public const string InvalidEmail = "Authentication.InvalidEmail";
        public const string UserLoginSuccess = "Authentication.UserLoginSuccess";
    }

    public static class BackgroundJobs
    {
        public const string JobEnqueueFailed = "BackgroundJobs.JobEnqueueFailed";
    }
}