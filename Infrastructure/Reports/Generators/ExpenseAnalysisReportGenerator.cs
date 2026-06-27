using Application.Interfaces.Database;
using Application.Interfaces.Reports;
using ClosedXML.Excel;
using Dapper;
using Infrastructure.Reports.Data;
using System.Data;

namespace Infrastructure.Reports.Generators;

internal sealed class ExpenseAnalysisReportGenerator(IDbExecutor db) : IReportGenerator
{
    public string ReportTypeKey => "ExpenseAnalysis";

    public async Task<byte[]> GenerateAsync(
        long              userId,
        long?             workspaceId,
        string            language,
        ReportParameters  parameters,
        CancellationToken ct = default)
    {
        var (categories, months, totals) = await FetchDataAsync(userId, workspaceId, parameters, ct);

        using var wb = new XLWorkbook();
        BuildCategorySheet(wb, language, parameters, categories, totals);
        BuildMonthlySheet(wb, language, parameters, months);

        wb.Style.Font.FontName = "Calibri";
        return ExcelStyles.SaveToBytes(wb);
    }

    private async Task<(IReadOnlyList<AnalysisCategoryRow>, IReadOnlyList<AnalysisMonthRow>, ExpenseAnalysisTotals)>
        FetchDataAsync(long userId, long? workspaceId, ReportParameters p, CancellationToken ct)
    {
        var dp = new DynamicParameters();
        dp.Add("@UserId",      userId,      DbType.Int64);
        dp.Add("@WorkspaceId", workspaceId, DbType.Int64);
        dp.Add("@DateFrom", p.DateFrom, DbType.Date);
        dp.Add("@DateTo",   p.DateTo,   DbType.Date);

        return await db.QueryMultipleAsync(
            "MyMoney.usp_Report_ExpenseAnalysis_GetData",
            async multi =>
            {
                var cats   = (await multi.ReadAsync<AnalysisCategoryRow>()).ToList();
                var months = (await multi.ReadAsync<AnalysisMonthRow>()).ToList();
                var totals = await multi.ReadSingleOrDefaultAsync<ExpenseAnalysisTotals>()
                             ?? new ExpenseAnalysisTotals();
                return (
                    (IReadOnlyList<AnalysisCategoryRow>)cats,
                    (IReadOnlyList<AnalysisMonthRow>)months,
                    totals);
            },
            dp, ct);
    }

    private static void BuildCategorySheet(
        XLWorkbook wb, string lang, ReportParameters p,
        IReadOnlyList<AnalysisCategoryRow> cats, ExpenseAnalysisTotals totals)
    {
        bool ar  = lang == "ar";
        var  ws  = wb.Worksheets.Add(ar ? "المصروفات حسب الفئة" : "Expenses by Category");
        int  row = 1;

        ws.Cell(row, 1).Value = ar ? "تحليل المصروفات" : "Expense Analysis";
        ws.Range(row, 1, row, 5).Merge();
        ExcelStyles.ApplyTitleStyle(ws.Cell(row, 1));
        ws.Row(row).Height = 28;
        row++;

        ws.Cell(row, 1).Value = $"{p.DateFrom:yyyy-MM-dd}  →  {p.DateTo:yyyy-MM-dd}";
        ws.Range(row, 1, row, 5).Merge();
        ExcelStyles.ApplySubtitleStyle(ws.Cell(row, 1));
        row += 2;

        ws.Cell(row, 1).Value = ar ? "إجمالي المصروفات" : "Total Expenses";
        ws.Cell(row, 2).Value = totals.TotalExpenses;
        ws.Cell(row, 3).Value = ar ? "إجمالي المعاملات" : "Total Transactions";
        ws.Cell(row, 4).Value = totals.TotalTransactions;
        ExcelStyles.ApplySummaryLabelStyle(ws.Cell(row, 1));
        ExcelStyles.ApplyNegativeAmountStyle(ws.Cell(row, 2));
        ExcelStyles.ApplySummaryLabelStyle(ws.Cell(row, 3));
        ExcelStyles.ApplySummaryValueStyle(ws.Cell(row, 4));
        ws.Row(row).Height = 20;
        row += 2;

        string[] headers = ar
            ? ["الفئة", "الإجمالي", "عدد المعاملات", "المتوسط", "النسبة %"]
            : ["Category", "Total", "Transactions", "Average", "Share %"];

        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(row, c + 1).Value = headers[c];
            ExcelStyles.ApplyHeaderStyle(ws.Cell(row, c + 1));
        }
        ws.Row(row).Height = 22;
        row++;

        for (int i = 0; i < cats.Count; i++)
        {
            var  cat = cats[i];
            bool alt = i % 2 == 1;

            ws.Cell(row, 1).Value = ar ? cat.CategoryNameAr : cat.CategoryNameEn;
            ws.Cell(row, 2).Value = cat.TotalAmount;
            ws.Cell(row, 3).Value = cat.TransactionCount;
            ws.Cell(row, 4).Value = cat.AvgAmount;
            ws.Cell(row, 5).Value = cat.Percentage / 100m;

            for (int c = 1; c <= 5; c++) ExcelStyles.ApplyDataStyle(ws.Cell(row, c), alt);
            ExcelStyles.ApplyNegativeAmountStyle(ws.Cell(row, 2));
            ExcelStyles.ApplyAmountFormat(ws.Cell(row, 4));
            ws.Cell(row, 5).Style.NumberFormat.Format = "0.00%";

            ws.Row(row).Height = 18;
            row++;
        }

        ws.Column(1).Width = 25;
        ws.Column(2).Width = 15;
        ws.Column(3).Width = 16;
        ws.Column(4).Width = 15;
        ws.Column(5).Width = 12;
    }

    private static void BuildMonthlySheet(
        XLWorkbook wb, string lang, ReportParameters p,
        IReadOnlyList<AnalysisMonthRow> months)
    {
        bool ar  = lang == "ar";
        var  ws  = wb.Worksheets.Add(ar ? "الاتجاه الشهري" : "Monthly Trend");
        int  row = 1;

        ws.Cell(row, 1).Value = ar ? "المصروفات الشهرية" : "Monthly Expenses";
        ws.Range(row, 1, row, 3).Merge();
        ExcelStyles.ApplyTitleStyle(ws.Cell(row, 1));
        ws.Row(row).Height = 28;
        row += 2;

        string[] headers = ar
            ? ["الشهر", "إجمالي المصروفات", "عدد المعاملات"]
            : ["Month",  "Total Expenses",    "Transactions"];

        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(row, c + 1).Value = headers[c];
            ExcelStyles.ApplyHeaderStyle(ws.Cell(row, c + 1));
        }
        ws.Row(row).Height = 22;
        row++;

        for (int i = 0; i < months.Count; i++)
        {
            var  m   = months[i];
            bool alt = i % 2 == 1;

            ws.Cell(row, 1).Value = $"{ExcelStyles.MonthName(m.Month, lang)} {m.Year}";
            ws.Cell(row, 2).Value = m.TotalAmount;
            ws.Cell(row, 3).Value = m.TransactionCount;

            for (int c = 1; c <= 3; c++) ExcelStyles.ApplyDataStyle(ws.Cell(row, c), alt);
            ExcelStyles.ApplyNegativeAmountStyle(ws.Cell(row, 2));
            ws.Row(row).Height = 18;
            row++;
        }

        ws.Column(1).Width = 20;
        ws.Column(2).Width = 16;
        ws.Column(3).Width = 16;
    }
}
