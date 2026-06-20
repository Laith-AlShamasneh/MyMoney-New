namespace Application.Features.Transaction.DbModels;

public class TransactionSearchDbModel
{
    public long      UserId     { get; set; }
    public byte?     TypeId     { get; set; }
    public int?      CategoryId { get; set; }
    public DateOnly? DateFrom   { get; set; }
    public DateOnly? DateTo     { get; set; }
    public decimal?  AmountMin  { get; set; }
    public decimal?  AmountMax  { get; set; }
    public string?   Search     { get; set; }
    public string    SortBy     { get; set; } = "TransactionDate";
    public string    SortDir    { get; set; } = "DESC";
    public int       PageNumber { get; set; } = 1;
    public int       PageSize   { get; set; } = 20;
}

public class TransactionSearchDbResult
{
    public IReadOnlyList<TransactionRowDbResult> Items        { get; set; } = [];
    public int     TotalCount    { get; set; }
    public decimal TotalIncome   { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal AvgAmount     { get; set; }
    public decimal MaxAmount     { get; set; }
}

public class TransactionRowDbResult
{
    public long      TransactionId     { get; set; }
    public int       CategoryId        { get; set; }
    public string    CategoryNameEn    { get; set; } = null!;
    public string    CategoryNameAr    { get; set; } = null!;
    public string?   CategoryIcon      { get; set; }
    public byte      TransactionTypeId { get; set; }
    public decimal   Amount            { get; set; }
    public string?   Description       { get; set; }
    public DateOnly  TransactionDate   { get; set; }
    public string?   Notes             { get; set; }
    public DateTime  CreatedAt         { get; set; }
    public DateTime? UpdatedAt         { get; set; }
}

public class TransactionByIdDbResult
{
    public long      TransactionId     { get; set; }
    public int       CategoryId        { get; set; }
    public string    CategoryNameEn    { get; set; } = null!;
    public string    CategoryNameAr    { get; set; } = null!;
    public string?   CategoryIcon      { get; set; }
    public byte      TransactionTypeId { get; set; }
    public decimal   Amount            { get; set; }
    public string?   Description       { get; set; }
    public DateOnly  TransactionDate   { get; set; }
    public string?   Notes             { get; set; }
    public DateTime  CreatedAt         { get; set; }
    public DateTime? UpdatedAt         { get; set; }
}

public class TransactionAnalyticsDbModel
{
    public long      UserId   { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo   { get; set; }
}

public class TransactionCategoryBreakdownDbResult
{
    public int     CategoryId  { get; set; }
    public string  NameEn      { get; set; } = null!;
    public string  NameAr      { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public int     TxCount     { get; set; }
    public decimal Percentage  { get; set; }
}

public class TransactionTrendPointDbResult
{
    public int     Year     { get; set; }
    public int     Month    { get; set; }
    public decimal Income   { get; set; }
    public decimal Expenses { get; set; }
}

public class TransactionAnalyticsDbResult
{
    public IReadOnlyList<TransactionCategoryBreakdownDbResult> CategoryBreakdown { get; set; } = [];
    public IReadOnlyList<TransactionTrendPointDbResult>        MonthlyTrend      { get; set; } = [];
}

public class CreateTransactionDbModel
{
    public long     UserId            { get; set; }
    public int      CategoryId        { get; set; }
    public byte     TransactionTypeId { get; set; }
    public decimal  Amount            { get; set; }
    public string?  Description       { get; set; }
    public DateOnly TransactionDate   { get; set; }
    public string?  Notes             { get; set; }
}

public class UpdateTransactionDbModel
{
    public long     UserId            { get; set; }
    public long     TransactionId     { get; set; }
    public int      CategoryId        { get; set; }
    public byte     TransactionTypeId { get; set; }
    public decimal  Amount            { get; set; }
    public string?  Description       { get; set; }
    public DateOnly TransactionDate   { get; set; }
    public string?  Notes             { get; set; }
}

public class DeleteTransactionDbModel
{
    public long UserId        { get; set; }
    public long TransactionId { get; set; }
}

public class UpdateTransactionDbResult
{
    public int      AffectedRows       { get; set; }
    public DateOnly OldTransactionDate { get; set; }
}

public class DeleteTransactionDbResult
{
    public int      AffectedRows { get; set; }
    public DateOnly DeletedDate  { get; set; }
}
