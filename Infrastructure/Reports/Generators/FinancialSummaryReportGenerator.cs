using Application.Interfaces.Database;
using Application.Interfaces.Reports;
using ClosedXML.Excel;
using Dapper;
using Infrastructure.Reports.Data;
using System.Data;

namespace Infrastructure.Reports.Generators;

internal sealed class FinancialSummaryReportGenerator(IDbExecutor db) : IReportGenerator
{
    public string ReportTypeKey => "FinancialSummary";

    public async Task<byte[]> GenerateAsync(
        long              userId,
        string            language,
        ReportParameters  parameters,
        CancellationToken ct = default)
    {
        var (months, totals) = await FetchDataAsync(userId, parameters, ct);

        using var wb = new XLWorkbook();

        BuildSummarySheet(wb, language, parameters, totals);
        BuildMonthlySheet(wb, language, parameters, months, totals);

        wb.Style.Font.FontName = "Calibri";
        return ExcelStyles.SaveToBytes(wb);
    }

    // ── Data ──────────────────────────────────────────────────────────────────

    private async Task<(IReadOnlyList<FinancialSummaryMonthRow> Months, FinancialSummaryTotals Totals)>
        FetchDataAsync(long userId, ReportParameters p, CancellationToken ct)
    {
        var dp = new DynamicParameters();
        dp.Add("@UserId",   userId,    DbType.Int64);
        dp.Add("@DateFrom", p.DateFrom, DbType.Date);
        dp.Add("@DateTo",   p.DateTo,   DbType.Date);

        return await db.QueryMultipleAsync(
            "MyMoney.usp_Report_FinancialSummary_GetData",
            async multi =>
            {
                var months = (await multi.ReadAsync<FinancialSummaryMonthRow>()).ToList();
                var totals = await multi.ReadSingleOrDefaultAsync<FinancialSummaryTotals>()
                             ?? new FinancialSummaryTotals();
                return ((IReadOnlyList<FinancialSummaryMonthRow>)months, totals);
            },
            dp, ct);
    }

    // ── Summary Sheet ─────────────────────────────────────────────────────────

    private static void BuildSummarySheet(
        XLWorkbook                  wb,
        string                      lang,
        ReportParameters            p,
        FinancialSummaryTotals      totals)
    {
        bool ar   = lang == "ar";
        var  ws   = wb.Worksheets.Add(ar ? "الملخص" : "Summary");
        int  row  = 1;

        // Title
        ws.Cell(row, 1).Value = ar ? "الملخص المالي" : "Financial Summary";
        ws.Range(row, 1, row, 4).Merge();
        ExcelStyles.ApplyTitleStyle(ws.Cell(row, 1));
        ws.Row(row).Height = 32;
        row++;

        // Sub-title (date range)
        var sub = $"{p.DateFrom:yyyy-MM-dd}  →  {p.DateTo:yyyy-MM-dd}";
        ws.Cell(row, 1).Value = sub;
        ws.Range(row, 1, row, 4).Merge();
        ExcelStyles.ApplySubtitleStyle(ws.Cell(row, 1));
        ws.Row(row).Height = 18;
        row += 2;

        // KPI cards
        AddKpiRow(ws, ref row, ar ? "إجمالي الدخل"       : "Total Income",
            totals.TotalIncome, positive: true);
        AddKpiRow(ws, ref row, ar ? "إجمالي المصروفات"   : "Total Expenses",
            totals.TotalExpenses, positive: false);
        AddKpiRow(ws, ref row, ar ? "صافي الرصيد"        : "Net Balance",
            totals.NetBalance, positive: totals.NetBalance >= 0);
        row++;

        // Stats
        AddStatRow(ws, ref row, ar ? "إجمالي المعاملات" : "Total Transactions",
            totals.TotalTransactions.ToString());
        AddStatRow(ws, ref row, ar ? "الأشهر النشطة"   : "Active Months",
            totals.ActiveMonths.ToString());

        // Column widths
        ws.Column(1).Width = 30;
        ws.Column(2).Width = 18;
        ws.Column(3).Width = 18;
        ws.Column(4).Width = 18;
    }

    private static void AddKpiRow(IXLWorksheet ws, ref int row, string label, decimal value, bool positive)
    {
        ws.Cell(row, 1).Value = label;
        ws.Cell(row, 2).Value = value;
        ExcelStyles.ApplySummaryLabelStyle(ws.Cell(row, 1));
        if (positive) ExcelStyles.ApplyPositiveAmountStyle(ws.Cell(row, 2));
        else          ExcelStyles.ApplyNegativeAmountStyle(ws.Cell(row, 2));
        ws.Range(row, 2, row, 4).Merge();
        ws.Row(row).Height = 22;
        row++;
    }

    private static void AddStatRow(IXLWorksheet ws, ref int row, string label, string value)
    {
        ws.Cell(row, 1).Value = label;
        ws.Cell(row, 2).Value = value;
        ExcelStyles.ApplySummaryLabelStyle(ws.Cell(row, 1));
        ExcelStyles.ApplySummaryValueStyle(ws.Cell(row, 2));
        ws.Range(row, 2, row, 4).Merge();
        ws.Row(row).Height = 20;
        row++;
    }

    // ── Monthly Sheet ─────────────────────────────────────────────────────────

    private static void BuildMonthlySheet(
        XLWorkbook                           wb,
        string                               lang,
        ReportParameters                     p,
        IReadOnlyList<FinancialSummaryMonthRow> months,
        FinancialSummaryTotals               totals)
    {
        bool ar  = lang == "ar";
        var  ws  = wb.Worksheets.Add(ar ? "التفاصيل الشهرية" : "Monthly Breakdown");
        int  row = 1;

        // Title
        ws.Cell(row, 1).Value = ar ? "التوزيع الشهري" : "Monthly Breakdown";
        ws.Range(row, 1, row, 5).Merge();
        ExcelStyles.ApplyTitleStyle(ws.Cell(row, 1));
        ws.Row(row).Height = 28;
        row += 2;

        // Headers
        string[] headers = ar
            ? ["الشهر", "الدخل", "المصروفات", "صافي الرصيد", "عدد المعاملات"]
            : ["Month",  "Income", "Expenses",  "Net Balance",  "Transactions"];

        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(row, c + 1).Value = headers[c];
            ExcelStyles.ApplyHeaderStyle(ws.Cell(row, c + 1));
        }
        ws.Row(row).Height = 22;
        row++;

        // Data rows
        for (int i = 0; i < months.Count; i++)
        {
            var m    = months[i];
            bool alt = i % 2 == 1;
            var label = $"{ExcelStyles.MonthName(m.Month, lang)} {m.Year}";

            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 2).Value = m.Income;
            ws.Cell(row, 3).Value = m.Expenses;
            ws.Cell(row, 4).Value = m.Net;
            ws.Cell(row, 5).Value = m.TransactionCount;

            ExcelStyles.ApplyDataStyle(ws.Cell(row, 1), alt);
            ExcelStyles.ApplyDataStyle(ws.Cell(row, 2), alt);
            ExcelStyles.ApplyDataStyle(ws.Cell(row, 3), alt);
            ExcelStyles.ApplyDataStyle(ws.Cell(row, 4), alt);
            ExcelStyles.ApplyDataStyle(ws.Cell(row, 5), alt);

            ExcelStyles.ApplyAmountFormat(ws.Cell(row, 2));
            ExcelStyles.ApplyAmountFormat(ws.Cell(row, 3));
            if (m.Net >= 0) ExcelStyles.ApplyPositiveAmountStyle(ws.Cell(row, 4));
            else            ExcelStyles.ApplyNegativeAmountStyle(ws.Cell(row, 4));

            ws.Row(row).Height = 18;
            row++;
        }

        // Totals row
        ws.Cell(row, 1).Value = ar ? "الإجمالي" : "Total";
        ws.Cell(row, 2).Value = totals.TotalIncome;
        ws.Cell(row, 3).Value = totals.TotalExpenses;
        ws.Cell(row, 4).Value = totals.NetBalance;
        ws.Cell(row, 5).Value = totals.TotalTransactions;

        for (int c = 1; c <= 5; c++) ExcelStyles.ApplyTotalStyle(ws.Cell(row, c));
        ExcelStyles.ApplyAmountFormat(ws.Cell(row, 2));
        ExcelStyles.ApplyAmountFormat(ws.Cell(row, 3));
        ExcelStyles.ApplyAmountFormat(ws.Cell(row, 4));

        // Column widths
        ws.Column(1).Width = 22;
        ws.Column(2).Width = 16;
        ws.Column(3).Width = 16;
        ws.Column(4).Width = 16;
        ws.Column(5).Width = 16;
    }
}
