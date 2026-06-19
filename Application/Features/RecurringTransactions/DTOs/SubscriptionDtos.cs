using System.Text.Json.Serialization;

namespace Application.Features.RecurringTransactions.DTOs;

// ── Requests ───────────────────────────────────────────────────────────────────

public record CreateSubscriptionRequest(
    int      CategoryId,
    string   Name,
    decimal  Amount,
    string?  Description,
    int      FrequencyId,
    int?     FrequencyInterval,
    int?     FrequencyUnit,
    string   StartDate,
    string?  EndDate,
    string   ProviderName,
    string?  WebsiteUrl,
    bool     AutoRenew,
    string?  RenewalDate,
    string?  TrialEndDate,
    string?  Notes);

public record UpdateSubscriptionRequest(
    long     Id,
    int      CategoryId,
    string   Name,
    decimal  Amount,
    string?  Description,
    string?  EndDate,
    string   ProviderName,
    string?  WebsiteUrl,
    bool     AutoRenew,
    string?  RenewalDate,
    string?  Notes);

public record GetSubscriptionsRequest(
    int? StatusId,
    int  PageNumber,
    int  PageSize);

// ── Responses ──────────────────────────────────────────────────────────────────

public sealed record SubscriptionResponse
{
    public long      Id               { get; init; }
    public string    Name             { get; init; } = string.Empty;
    public decimal   Amount           { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string?   Description      { get; init; }
    public int       FrequencyId      { get; init; }
    public string    FrequencyLabel   { get; init; } = string.Empty;
    public int       StatusId         { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateOnly? NextGenerationDate { get; init; }
    public string    ProviderName     { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string?   WebsiteUrl       { get; init; }
    public bool      AutoRenew        { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateOnly? RenewalDate      { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateOnly? TrialEndDate     { get; init; }
    public string    CategoryNameEn   { get; init; } = string.Empty;
    public string    CategoryNameAr   { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string?   Notes            { get; init; }
    public DateOnly  StartDate        { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateOnly? EndDate          { get; init; }
    public DateTime  CreatedAt        { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? UpdatedAt        { get; init; }
}

public sealed record SubscriptionListItemResponse
{
    public long      Id              { get; init; }
    public string    Name            { get; init; } = string.Empty;
    public decimal   Amount          { get; init; }
    public int       StatusId        { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateOnly? RenewalDate     { get; init; }
    public string    ProviderName    { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string?   WebsiteUrl      { get; init; }
    public bool      AutoRenew       { get; init; }
    public int       FrequencyId     { get; init; }
    public string    FrequencyLabel  { get; init; } = string.Empty;
    public string    CategoryNameEn  { get; init; } = string.Empty;
    public string    CategoryNameAr  { get; init; } = string.Empty;
}
