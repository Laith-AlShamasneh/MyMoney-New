namespace Application.Features.Profile.DTOs;

public sealed record RequestEmailChangeRequest(
    string NewEmail,
    string CurrentPassword
);

public sealed record ConfirmEmailChangeRequest(string Token);
