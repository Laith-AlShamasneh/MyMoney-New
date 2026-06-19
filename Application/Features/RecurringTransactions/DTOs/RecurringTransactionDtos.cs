using System.Text.Json.Serialization;

namespace Application.Features.RecurringTransactions.DTOs;

// ── Requests ───────────────────────────────────────────────────────────────────

public record RecurringTransactionIdRequest(long Id);

public record CreateRecurringTransactionRequest(
    int      CategoryId,
    int      TransactionTypeId,
    string   Name,
    decimal  Amount,
    string?  Description,
    int      FrequencyId,
    int?     FrequencyInterval,
    int?     FrequencyUnit,
    int?     DayOfMonth,
    int?     DayOfWeek,
    string   StartDate,
    string?  EndDate,
    string?  Notes);

public record UpdateRecurringTransactionRequest(
    long     Id,
    int      CategoryId,
    string   Name,
    decimal  Amount,
    string?  Description,
    int?     FrequencyInterval,
    int?     FrequencyUnit,
    int?     DayOfMonth,
    int?     DayOfWeek,
    string?  EndDate,
    string?  Notes);

public record GetRecurringTransactionsRequest(
    int? StatusId,
    int? TransactionTypeId,
    int  PageNumber,
    int  PageSize);

// ── Responses ──────────────────────────────────────────────────────────────────

public sealed record RecurringTransactionResponse
{
    public long      Id                  { get; init; }
    public int       CategoryId          { get; init; }
    public string    CategoryNameEn      { get; init; } = string.Empty;
    public string    CategoryNameAr      { get; init; } = string.Empty;
    public int       TransactionTypeId   { get; init; }
    public string    Name                { get; init; } = string.Empty;
    public decimal   Amount              { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string?   Description         { get; init; }
    public int       FrequencyId         { get; init; }
    public string    FrequencyLabel      { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int?      FrequencyInterval   { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int?      FrequencyUnit       { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int?      DayOfMonth          { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int?      DayOfWeek           { get; init; }
    public DateOnly  StartDate           { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateOnly? EndDate             { get; init; }
    public int       StatusId            { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateOnly? LastGeneratedDate   { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateOnly? NextGenerationDate  { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string?   Notes               { get; init; }
    public bool      IsSubscription      { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubscriptionMetadataDto? Subscription { get; init; }
    public DateTime  CreatedAt           { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? UpdatedAt           { get; init; }
}

public record SubscriptionMetadataDto(
    string   ProviderName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string?  WebsiteUrl,
    bool     AutoRenew,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateOnly? RenewalDate,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateOnly? TrialEndDate);

public sealed record RecurringTransactionListItemResponse
{
    public long      Id                  { get; init; }
    public string    Name                { get; init; } = string.Empty;
    public decimal   Amount              { get; init; }
    public int       TransactionTypeId   { get; init; }
    public int       FrequencyId         { get; init; }
    public string    FrequencyLabel      { get; init; } = string.Empty;
    public int       StatusId            { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateOnly? NextGenerationDate  { get; init; }
    public string    CategoryNameEn      { get; init; } = string.Empty;
    public string    CategoryNameAr      { get; init; } = string.Empty;
    public bool      IsSubscription      { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string?   ProviderName        { get; init; }
}

public record UpcomingPaymentDto(
    long     Id,
    string   Name,
    decimal  Amount,
    DateOnly DueDate,
    int      DaysUntil,
    string   CategoryNameEn,
    string   CategoryNameAr);

public record UpcomingRenewalDto(
    long     Id,
    string   Name,
    decimal  Amount,
    DateOnly RenewalDate,
    int      DaysUntil,
    string   ProviderName);

public sealed record RecurringTransactionDashboardResponse
{
    public decimal                          MonthlyRecurringIncome    { get; init; }
    public decimal                          MonthlyRecurringExpenses  { get; init; }
    public int                              ActiveDefinitionsCount    { get; init; }
    public int                              ActiveSubscriptionsCount  { get; init; }
    public IReadOnlyList<UpcomingPaymentDto>  UpcomingPayments        { get; init; } = [];
    public IReadOnlyList<UpcomingRenewalDto>  UpcomingRenewals        { get; init; } = [];
}
