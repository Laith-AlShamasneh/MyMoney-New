namespace Application.Features.Transaction.DTOs;

// ── Search ────────────────────────────────────────────────────────────────────

public record SearchTransactionsRequest(
    int?     TypeId,
    int?     CategoryId,
    string?  DateFrom,
    string?  DateTo,
    decimal? AmountMin,
    decimal? AmountMax,
    string?  Search,
    string?  SortBy,
    string?  SortDir,
    int      PageNumber,
    int      PageSize);

public record TransactionSearchResponse(
    TransactionSummaryDto            Summary,
    IReadOnlyList<TransactionItemDto> Items,
    int                               TotalCount,
    int                               PageNumber,
    int                               PageSize);

public record TransactionSummaryDto(
    int     TotalCount,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal NetAmount,
    decimal AvgAmount,
    decimal MaxAmount);

public record TransactionItemDto(
    long     TransactionId,
    int      CategoryId,
    string   CategoryNameEn,
    string   CategoryNameAr,
    string?  CategoryIcon,
    int      TransactionTypeId,
    decimal  Amount,
    string?  Description,
    DateOnly TransactionDate,
    string?  Notes,
    DateTime CreatedAt);

// ── Analytics ────────────────────────────────────────────────────────────────

public record GetAnalyticsRequest(
    string? DateFrom,
    string? DateTo);

public record TransactionAnalyticsResponse(
    IReadOnlyList<CategoryBreakdownDto> CategoryBreakdown,
    IReadOnlyList<TrendPointDto>         MonthlyTrend);

public record CategoryBreakdownDto(
    int     CategoryId,
    string  NameEn,
    string  NameAr,
    decimal TotalAmount,
    int     TxCount,
    decimal Percentage);

public record TrendPointDto(int Year, int Month, decimal Income, decimal Expenses);

// ── Single transaction ────────────────────────────────────────────────────────

public record TransactionDetailResponse(
    long     TransactionId,
    int      CategoryId,
    string   CategoryNameEn,
    string   CategoryNameAr,
    string?  CategoryIcon,
    int      TransactionTypeId,
    decimal  Amount,
    string?  Description,
    DateOnly TransactionDate,
    string?  Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

// ── Create ────────────────────────────────────────────────────────────────────

public record CreateTransactionRequest(
    int      CategoryId,
    int      TransactionTypeId,
    decimal  Amount,
    string?  Description,
    string   TransactionDate,
    string?  Notes);

public record CreateTransactionResponse(long TransactionId);

// ── Update ────────────────────────────────────────────────────────────────────

public record UpdateTransactionRequest(
    int      CategoryId,
    int      TransactionTypeId,
    decimal  Amount,
    string?  Description,
    string   TransactionDate,
    string?  Notes);
