namespace Infrastructure.Reports.Data;

// Financial Summary
internal sealed class FinancialSummaryMonthRow
{
    public int     Year             { get; set; }
    public int     Month            { get; set; }
    public decimal Income           { get; set; }
    public decimal Expenses         { get; set; }
    public decimal Net              { get; set; }
    public int     TransactionCount { get; set; }
}

internal sealed class FinancialSummaryTotals
{
    public decimal TotalIncome       { get; set; }
    public decimal TotalExpenses     { get; set; }
    public decimal NetBalance        { get; set; }
    public int     TotalTransactions { get; set; }
    public int     ActiveMonths      { get; set; }
}

// Transaction Detail
internal sealed class TransactionDetailRow
{
    public long     TransactionId   { get; set; }
    public DateOnly TransactionDate { get; set; }
    public string   Description     { get; set; } = null!;
    public string   CategoryNameEn  { get; set; } = null!;
    public string   CategoryNameAr  { get; set; } = null!;
    public string   TransactionType { get; set; } = null!;
    public decimal  Amount          { get; set; }
    public string   Notes           { get; set; } = null!;
}

internal sealed class TransactionDetailSummary
{
    public int     TotalCount    { get; set; }
    public decimal TotalIncome   { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal AvgAmount     { get; set; }
}

// Income / Expense Analysis (shared shape)
internal sealed class AnalysisCategoryRow
{
    public string  CategoryNameEn  { get; set; } = null!;
    public string  CategoryNameAr  { get; set; } = null!;
    public decimal TotalAmount     { get; set; }
    public int     TransactionCount { get; set; }
    public decimal AvgAmount       { get; set; }
    public decimal Percentage      { get; set; }
}

internal sealed class AnalysisMonthRow
{
    public int     Year             { get; set; }
    public int     Month            { get; set; }
    public decimal TotalAmount      { get; set; }
    public int     TransactionCount { get; set; }
}

internal sealed class IncomeAnalysisTotals
{
    public decimal TotalIncome       { get; set; }
    public int     TotalTransactions { get; set; }
}

internal sealed class ExpenseAnalysisTotals
{
    public decimal TotalExpenses     { get; set; }
    public int     TotalTransactions { get; set; }
}

// Category Analysis
internal sealed class CategoryAnalysisRow
{
    public string  CategoryNameEn   { get; set; } = null!;
    public string  CategoryNameAr   { get; set; } = null!;
    public string  TransactionType  { get; set; } = null!;
    public decimal TotalAmount      { get; set; }
    public int     TransactionCount { get; set; }
    public decimal AvgAmount        { get; set; }
    public decimal MaxAmount        { get; set; }
    public decimal MinAmount        { get; set; }
}

internal sealed class CategoryAnalysisSummary
{
    public int     UniqueCategories  { get; set; }
    public int     TotalTransactions { get; set; }
    public decimal TotalIncome       { get; set; }
    public decimal TotalExpenses     { get; set; }
}
