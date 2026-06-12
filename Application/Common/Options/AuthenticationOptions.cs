namespace Application.Common.Options;

public sealed class AuthenticationOptions
{
    public int MaxFailedLoginAttempts   { get; init; } = 5;
    public int LockoutDurationMinutes   { get; init; } = 30;
}
