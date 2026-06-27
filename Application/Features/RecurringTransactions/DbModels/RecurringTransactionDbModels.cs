namespace Application.Features.RecurringTransactions.DbModels;

// ── Create ─────────────────────────────────────────────────────────────────────

public class CreateRecurringTransactionDbModel
{
    public long      UserId             { get; set; }
    public long?     WorkspaceId        { get; set; }
    public int       CategoryId         { get; set; }
    public byte      TransactionTypeId  { get; set; }
    public string    Name               { get; set; } = null!;
    public decimal   Amount             { get; set; }
    public string?   Description        { get; set; }
    public byte      FrequencyId        { get; set; }
    public int?      FrequencyInterval  { get; set; }
    public byte?     FrequencyUnit      { get; set; }
    public byte?     DayOfMonth         { get; set; }
    public byte?     DayOfWeek          { get; set; }
    public DateOnly  StartDate          { get; set; }
    public DateOnly? EndDate            { get; set; }
    public bool      IsSubscription     { get; set; }
    public string?   Notes              { get; set; }
    public DateOnly  NextGenerationDate { get; set; }
    // Subscription fields — only populated when IsSubscription = true
    public string?   ProviderName       { get; set; }
    public string?   WebsiteUrl         { get; set; }
    public bool?     AutoRenew          { get; set; }
    public DateOnly? RenewalDate        { get; set; }
    public DateOnly? TrialEndDate       { get; set; }
}

// ── Update ─────────────────────────────────────────────────────────────────────

public class UpdateRecurringTransactionDbModel
{
    public long      Id                 { get; set; }
    public long      UserId             { get; set; }
    public long?     WorkspaceId        { get; set; }
    public int       CategoryId         { get; set; }
    public string    Name               { get; set; } = null!;
    public decimal   Amount             { get; set; }
    public string?   Description        { get; set; }
    public int?      FrequencyInterval  { get; set; }
    public byte?     FrequencyUnit      { get; set; }
    public byte?     DayOfMonth         { get; set; }
    public byte?     DayOfWeek          { get; set; }
    public DateOnly? EndDate            { get; set; }
    public string?   Notes              { get; set; }
    public DateOnly  NextGenerationDate { get; set; }
    // Subscription fields
    public string?   ProviderName       { get; set; }
    public string?   WebsiteUrl         { get; set; }
    public bool?     AutoRenew          { get; set; }
    public DateOnly? RenewalDate        { get; set; }
    public DateOnly? TrialEndDate       { get; set; }
}

// ── Read result ────────────────────────────────────────────────────────────────

public class RecurringTransactionDbResult
{
    public long      Id                  { get; set; }
    public long      UserId              { get; set; }
    public int       CategoryId          { get; set; }
    public string    CategoryNameEn      { get; set; } = null!;
    public string    CategoryNameAr      { get; set; } = null!;
    public byte      TransactionTypeId   { get; set; }
    public string    Name                { get; set; } = null!;
    public decimal   Amount              { get; set; }
    public string?   Description         { get; set; }
    public byte      FrequencyId         { get; set; }
    public int?      FrequencyInterval   { get; set; }
    public byte?     FrequencyUnit       { get; set; }
    public byte?     DayOfMonth          { get; set; }
    public byte?     DayOfWeek           { get; set; }
    public DateOnly  StartDate           { get; set; }
    public DateOnly? EndDate             { get; set; }
    public byte      StatusId            { get; set; }
    public bool      IsSubscription      { get; set; }
    public DateOnly? LastGeneratedDate   { get; set; }
    public DateOnly? NextGenerationDate  { get; set; }
    public string?   Notes               { get; set; }
    public DateTime  CreatedAt           { get; set; }
    public DateTime? UpdatedAt           { get; set; }
    // Subscription metadata (NULL for non-subscriptions)
    public string?   ProviderName        { get; set; }
    public string?   WebsiteUrl          { get; set; }
    public bool?     AutoRenew           { get; set; }
    public DateOnly? RenewalDate         { get; set; }
    public DateOnly? TrialEndDate        { get; set; }
}

public class GetRecurringTransactionsDbModel
{
    public long  UserId            { get; set; }
    public long? WorkspaceId       { get; set; }
    public byte? StatusId          { get; set; }
    public byte? TransactionTypeId { get; set; }
    public bool? IsSubscription    { get; set; }
    public int   PageNumber        { get; set; } = 1;
    public int   PageSize          { get; set; } = 20;
}

public class GetRecurringTransactionsDbResult
{
    public IReadOnlyList<RecurringTransactionDbResult> Items      { get; set; } = [];
    public int                                          TotalCount { get; set; }
}

// ── Due definitions (scheduler) ────────────────────────────────────────────────

public class RecurringTransactionDueDbResult
{
    public long      Id                 { get; set; }
    public long      UserId             { get; set; }
    public int       CategoryId         { get; set; }
    public byte      TransactionTypeId  { get; set; }
    public string    Name               { get; set; } = null!;
    public decimal   Amount             { get; set; }
    public string?   Description        { get; set; }
    public byte      FrequencyId        { get; set; }
    public int?      FrequencyInterval  { get; set; }
    public byte?     FrequencyUnit      { get; set; }
    public byte?     DayOfMonth         { get; set; }
    public byte?     DayOfWeek          { get; set; }
    public DateOnly  StartDate          { get; set; }
    public DateOnly  NextGenerationDate { get; set; }
    public DateOnly? LastGeneratedDate  { get; set; }
    public DateOnly? EndDate            { get; set; }
    public bool      IsSubscription     { get; set; }
    public string?   ProviderName       { get; set; }
}

// ── Generation input ───────────────────────────────────────────────────────────

public class GenerateTransactionDbModel
{
    public long     DefinitionId       { get; set; }
    public DateOnly ForDate            { get; set; }
    public DateOnly NextGenerationDate { get; set; }
}

// ── Generation output ──────────────────────────────────────────────────────────

public class GenerateTransactionDbResult
{
    public long TransactionId  { get; set; }
    public bool WasAlreadyDone { get; set; }
}

// ── Dashboard summary ──────────────────────────────────────────────────────────

public class RecurringTransactionDashboardDbResult
{
    public decimal MonthlyRecurringIncome   { get; set; }
    public decimal MonthlyRecurringExpenses { get; set; }
    public int     ActiveDefinitionsCount   { get; set; }
    public int     ActiveSubscriptionsCount { get; set; }
}

public class UpcomingItemDbResult
{
    public long      Id              { get; set; }
    public long      UserId          { get; set; }
    public string    Name            { get; set; } = null!;
    public decimal   Amount          { get; set; }
    public DateOnly  NextDate        { get; set; }
    public int       DaysUntil       { get; set; }
    public string    CategoryNameEn  { get; set; } = null!;
    public string    CategoryNameAr  { get; set; } = null!;
    public bool      IsSubscription  { get; set; }
    public string?   ProviderName    { get; set; }
}
