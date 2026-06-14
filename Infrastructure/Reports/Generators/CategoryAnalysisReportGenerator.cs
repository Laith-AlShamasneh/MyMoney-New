using Application.Interfaces.Database;
using Application.Interfaces.Reports;
using ClosedXML.Excel;
using Dapper;
using Infrastructure.Reports.Data;
using System.Data;

namespace Infrastructure.Reports.Generators;

internal sealed class CategoryAnalysisReportGenerator(IDbExecutor db) : IReportGenerator
{
    public string ReportTypeKey => "CategoryAnalysis";

    public async Task<byte[]> GenerateAsync(
        long              userId,
        string            language,
        ReportParameters  parameters,
        CancellationToken ct = default)
    {
        var (rows, summary) = await FetchDataAsync(userId, parameters, ct);

        using var wb = new XLWorkbook();
        BuildDetailSheet(wb, language, parameters, rows);
        BuildSummarySheet(wb, language, parameters, summary);

        wb.Style.Font.FontName = "Calibri";
        return ExcelStyles.SaveToBytes(wb);
    }

    private async Task<(IReadOnlyList<CategoryAnalysisRow>, CategoryAnalysisSummary)>
        FetchDataAsync(long userId, ReportParameters p, CancellationToken ct)
    {
        var dp = new DynamicParameters();
        dp.Add("@UserId",   userId,    DbType.Int64);
        dp.Add("@DateFrom", p.DateFrom, DbType.Date);
        dp.Add("@DateTo",   p.DateTo,   DbType.Date);

        return await db.QueryMultipleAsync(
            "MyMoney.usp_Report_CategoryAnalysis_GetData",
            async multi =>
            {
                var rows    = (await multi.ReadAsync<CategoryAnalysisRow>()).ToList();
                var summary = await multi.ReadSingleOrDefaultAsync<CategoryAnalysisSummary>()
                              ?? new CategoryAnalysisSummary();
                return ((IReadOnlyList<CategoryAnalysisRow>)rows, summary);
            },
            dp, ct);
    }

    private static void BuildDetailSheet(
        XLWorkbook wb, string lang, ReportParameters p,
        IReadOnlyList<CategoryAnalysisRow> rows)
    {
        bool ar  = lang == "ar";
        var  ws  = wb.Worksheets.Add(ar ? "أداء الفئات" : "Category Performance");
        int  row = 1;

        ws.Cell(row, 1).Value = ar ? "تحليل الفئات" : "Category Analysis";
        ws.Range(row, 1, row, 7).Merge();
        ExcelStyles.ApplyTitleStyle(ws.Cell(row, 1));
        ws.Row(row).Height = 28;
        row++;

        ws.Cell(row, 1).Value = $"{p.DateFrom:yyyy-MM-dd}  →  {p.DateTo:yyyy-MM-dd}";
        ws.Range(row, 1, row, 7).Merge();
        ExcelStyles.ApplySubtitleStyle(ws.Cell(row, 1));
        row += 2;

        string[] headers = ar
            ? ["الفئة", "النوع", "الإجمالي", "عدد المعاملات", "المتوسط", "الحد الأقصى", "الحد الأدنى"]
            : ["Category", "Type", "Total", "Transactions", "Average", "Maximum", "Minimum"];

        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(row, c + 1).Value = headers[c];
            ExcelStyles.ApplyHeaderStyle(ws.Cell(row, c + 1));
        }
        ws.Row(row).Height = 22;
        row++;

        for (int i = 0; i < rows.Count; i++)
        {
            var  r    = rows[i];
            bool alt  = i % 2 == 1;
            bool inc  = r.TransactionType == "Income";

            ws.Cell(row, 1).Value = ar ? r.CategoryNameAr : r.CategoryNameEn;
            ws.Cell(row, 2).Value = ar ? (inc ? "دخل" : "مصروف") : r.TransactionType;
            ws.Cell(row, 3).Value = r.TotalAmount;
            ws.Cell(row, 4).Value = r.TransactionCount;
            ws.Cell(row, 5).Value = r.AvgAmount;
            ws.Cell(row, 6).Value = r.MaxAmount;
            ws.Cell(row, 7).Value = r.MinAmount;

            for (int c = 1; c <= 7; c++) ExcelStyles.ApplyDataStyle(ws.Cell(row, c), alt);

            if (inc) ExcelStyles.ApplyPositiveAmountStyle(ws.Cell(row, 3));
            else     ExcelStyles.ApplyNegativeAmountStyle(ws.Cell(row, 3));
            ExcelStyles.ApplyAmountFormat(ws.Cell(row, 5));
            ExcelStyles.ApplyAmountFormat(ws.Cell(row, 6));
            ExcelStyles.ApplyAmountFormat(ws.Cell(row, 7));

            ws.Row(row).Height = 18;
            row++;
        }

        ws.Column(1).Width = 24;
        ws.Column(2).Width = 12;
        ws.Column(3).Width = 14;
        ws.Column(4).Width = 16;
        ws.Column(5).Width = 14;
        ws.Column(6).Width = 14;
        ws.Column(7).Width = 14;
    }

    private static void BuildSummarySheet(
        XLWorkbook wb, string lang, ReportParameters p,
        CategoryAnalysisSummary summary)
    {
        bool ar  = lang == "ar";
        var  ws  = wb.Worksheets.Add(ar ? "الملخص" : "Summary");
        int  row = 1;

        ws.Cell(row, 1).Value = ar ? "ملخص الفئات" : "Category Summary";
        ws.Range(row, 1, row, 3).Merge();
        ExcelStyles.ApplyTitleStyle(ws.Cell(row, 1));
        ws.Row(row).Height = 28;
        row += 2;

        void AddInt(string label, int value)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 2).Value = value;
            ws.Range(row, 2, row, 3).Merge();
            ExcelStyles.ApplySummaryLabelStyle(ws.Cell(row, 1));
            ExcelStyles.ApplySummaryValueStyle(ws.Cell(row, 2));
            ws.Row(row).Height = 20;
            row++;
        }

        void AddAmount(string label, decimal value)
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

        AddInt(ar ? "الفئات النشطة"     : "Active Categories",   summary.UniqueCategories);
        AddInt(ar ? "إجمالي المعاملات" : "Total Transactions",   summary.TotalTransactions);
        AddAmount(ar ? "إجمالي الدخل"     : "Total Income",   summary.TotalIncome);
        AddAmount(ar ? "إجمالي المصروفات" : "Total Expenses", summary.TotalExpenses);

        ws.Column(1).Width = 25;
        ws.Column(2).Width = 16;
        ws.Column(3).Width = 16;
    }
}
