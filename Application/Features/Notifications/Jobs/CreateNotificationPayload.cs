namespace Application.Features.Notifications.Jobs;

public sealed record CreateNotificationPayload(
    string                       TemplateCode,
    long                         UserId,
    Dictionary<string, string>?  Parameters,
    string?                      PayloadJson);
