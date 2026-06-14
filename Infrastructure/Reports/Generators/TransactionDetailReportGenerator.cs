using Application.Interfaces.Database;
using Application.Interfaces.Reports;
using ClosedXML.Excel;
using Dapper;
using Infrastructure.Reports.Data;
using System.Data;

namespace Infrastructure.Reports.Generators;

internal sealed class TransactionDetailReportGenerator(IDbExecutor db) : IReportGenerator
{
    public string ReportTypeKey => "TransactionDetail";

    public async Task<byte[]> GenerateAsync(
        long              userId,
        string            language,
        ReportParameters  parameters,
        CancellationToken ct = default)
    {
        var (rows, summary) = await FetchDataAsync(userId, parameters, ct);

        using var wb = new XLWorkbook();

        BuildTransactionsSheet(wb, language, parameters, rows);
        BuildSummarySheet(wb, language, parameters, summary);

        wb.Style.Font.FontName = "Calibri";
        return ExcelStyles.SaveToBytes(wb);
    }

    private async Task<(IReadOnlyList<TransactionDetailRow>, TransactionDetailSummary)>
        FetchDataAsync(long userId, ReportParameters p, CancellationToken ct)
    {
        var dp = new DynamicParameters();
        dp.Add("@UserId",   userId,    DbType.Int64);
        dp.Add("@DateFrom", p.DateFrom, DbType.Date);
        dp.Add("@DateTo",   p.DateTo,   DbType.Date);

        return await db.QueryMultipleAsync(
            "MyMoney.usp_Report_TransactionDetail_GetData",
            async multi =>
            {
                var rows    = (await multi.ReadAsync<TransactionDetailRow>()).ToList();
                var summary = await multi.ReadSingleOrDefaultAsync<TransactionDetailSummary>()
                              ?? new TransactionDetailSummary();
                return ((IReadOnlyList<TransactionDetailRow>)rows, summary);
            },
            dp, ct);
    }

    private static void BuildTransactionsSheet(
        XLWorkbook                      wb,
        string                          lang,
        ReportParameters                p,
        IReadOnlyList<TransactionDetailRow> rows)
    {
        bool ar  = lang == "ar";
        var  ws  = wb.Worksheets.Add(ar ? "المعاملات" : "Transactions");
        int  row = 1;

        ws.Cell(row, 1).Value = ar ? "تفاصيل المعاملات" : "Transaction Detail";
        ws.Range(row, 1, row, 6).Merge();
        ExcelStyles.ApplyTitleStyle(ws.Cell(row, 1));
        ws.Row(row).Height = 28;
        row++;

        ws.Cell(row, 1).Value = $"{p.DateFrom:yyyy-MM-dd}  →  {p.DateTo:yyyy-MM-dd}";
        ws.Range(row, 1, row, 6).Merge();
        ExcelStyles.ApplySubtitleStyle(ws.Cell(row, 1));
        ws.Row(row).Height = 16;
        row += 2;

        string[] headers = ar
            ? ["التاريخ", "الوصف", "الفئة", "النوع", "المبلغ", "ملاحظات"]
            : ["Date",    "Description", "Category", "Type", "Amount", "Notes"];

        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(row, c + 1).Value = headers[c];
            ExcelStyles.ApplyHeaderStyle(ws.Cell(row, c + 1));
        }
        ws.Row(row).Height = 22;
        row++;

        for (int i = 0; i < rows.Count; i++)
        {
            var  r   = rows[i];
            bool alt = i % 2 == 1;

            ws.Cell(row, 1).Value = r.TransactionDate.ToString("yyyy-MM-dd");
            ws.Cell(row, 2).Value = r.Description;
            ws.Cell(row, 3).Value = ar ? r.CategoryNameAr : r.CategoryNameEn;
            ws.Cell(row, 4).Value = ar
                ? (r.TransactionType == "Income" ? "دخل" : "مصروف")
                : r.TransactionType;
            ws.Cell(row, 5).Value = r.Amount;
            ws.Cell(row, 6).Value = r.Notes;

            for (int c = 1; c <= 6; c++) ExcelStyles.ApplyDataStyle(ws.Cell(row, c), alt);
            ExcelStyles.ApplyAmountFormat(ws.Cell(row, 5));
            if (r.TransactionType == "Income")
                ExcelStyles.ApplyPositiveAmountStyle(ws.Cell(row, 5));
            else
                ExcelStyles.ApplyNegativeAmountStyle(ws.Cell(row, 5));

            ws.Row(row).Height = 18;
            row++;
        }

        ws.Column(1).Width = 14;
        ws.Column(2).Width = 30;
        ws.Column(3).Width = 20;
        ws.Column(4).Width = 12;
        ws.Column(5).Width = 14;
        ws.Column(6).Width = 30;
    }

    private static void BuildSummarySheet(
        XLWorkbook               wb,
        string                   lang,
        ReportParameters         p,
        TransactionDetailSummary summary)
    {
        bool ar  = lang == "ar";
        var  ws  = wb.Worksheets.Add(ar ? "الملخص" : "Summary");
        int  row = 1;

        ws.Cell(row, 1).Value = ar ? "ملخص المعاملات" : "Transaction Summary";
        ws.Range(row, 1, row, 3).Merge();
        ExcelStyles.ApplyTitleStyle(ws.Cell(row, 1));
        ws.Row(row).Height = 28;
        row += 2;

        void AddIntRow(string label, int value)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 2).Value = value;
            ws.Range(row, 2, row, 3).Merge();
            ExcelStyles.ApplySummaryLabelStyle(ws.Cell(row, 1));
            ExcelStyles.ApplySummaryValueStyle(ws.Cell(row, 2));
            ws.Row(row).Height = 20;
            row++;
        }

        void AddAmountRow(string label, decimal value)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 2).Value = value;
            ws.Range(row, 2, row, 3).Merge();
            ExcelStyles.ApplySummaryLabelStyle(ws.Cell(row, 1));
            ExcelStyles.ApplySummaryValueStyle(ws.Cell(row, 2));
            ExcelStyles.ApplyAmountFormat(ws.Cell(row, 2));
            ws.Row(row).Height = 20;
            row++;
        }

        AddIntRow(ar ? "إجمالي المعاملات" : "Total Transactions", summary.TotalCount);
        AddAmountRow(ar ? "إجمالي الدخل"     : "Total Income",       summary.TotalIncome);
        AddAmountRow(ar ? "إجمالي المصروفات" : "Total Expenses",     summary.TotalExpenses);
        AddAmountRow(ar ? "متوسط المبلغ"      : "Average Amount",     summary.AvgAmount);

        ws.Column(1).Width = 25;
        ws.Column(2).Width = 16;
        ws.Column(3).Width = 16;
    }
}
