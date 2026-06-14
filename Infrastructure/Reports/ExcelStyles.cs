using ClosedXML.Excel;

namespace Infrastructure.Reports;

internal static class ExcelStyles
{
    // Brand colours
    public const string PrimaryColor    = "2563EB"; // blue-600
    public const string SuccessColor    = "16A34A"; // green-600
    public const string DangerColor     = "DC2626"; // red-600
    public const string WarningColor    = "D97706"; // amber-600
    public const string MutedColor      = "64748B"; // slate-500
    public const string HeaderBg        = "1E3A8A"; // blue-900
    public const string HeaderFg        = "FFFFFF";
    public const string AltRowBg        = "EFF6FF"; // blue-50
    public const string TotalRowBg      = "DBEAFE"; // blue-100
    public const string SummaryBg       = "F1F5F9"; // slate-100
    public const string BorderColor     = "CBD5E1"; // slate-300

    public static void ApplyHeaderStyle(IXLCell cell, bool dark = true)
    {
        cell.Style.Font.Bold      = true;
        cell.Style.Font.FontColor = XLColor.FromHtml(dark ? HeaderFg : HeaderBg);
        cell.Style.Font.FontSize  = 11;
        if (dark)
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(HeaderBg);
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        cell.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
        ApplyBorder(cell);
    }

    public static void ApplyDataStyle(IXLCell cell, bool alternate = false)
    {
        if (alternate)
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(AltRowBg);
        cell.Style.Font.FontSize = 10;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ApplyBorder(cell);
    }

    public static void ApplyTotalStyle(IXLCell cell)
    {
        cell.Style.Fill.BackgroundColor = XLColor.FromHtml(TotalRowBg);
        cell.Style.Font.Bold     = true;
        cell.Style.Font.FontSize = 10;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ApplyBorder(cell);
    }

    public static void ApplySummaryLabelStyle(IXLCell cell)
    {
        cell.Style.Font.Bold      = true;
        cell.Style.Font.FontColor = XLColor.FromHtml(MutedColor);
        cell.Style.Font.FontSize  = 10;
        cell.Style.Fill.BackgroundColor = XLColor.FromHtml(SummaryBg);
        ApplyBorder(cell);
    }

    public static void ApplySummaryValueStyle(IXLCell cell)
    {
        cell.Style.Font.Bold     = true;
        cell.Style.Font.FontSize = 11;
        cell.Style.Fill.BackgroundColor = XLColor.FromHtml(SummaryBg);
        ApplyBorder(cell);
    }

    public static void ApplyTitleStyle(IXLCell cell)
    {
        cell.Style.Font.Bold      = true;
        cell.Style.Font.FontSize  = 16;
        cell.Style.Font.FontColor = XLColor.FromHtml(HeaderBg);
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        cell.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
    }

    public static void ApplySubtitleStyle(IXLCell cell)
    {
        cell.Style.Font.FontSize  = 10;
        cell.Style.Font.FontColor = XLColor.FromHtml(MutedColor);
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    public static void ApplyPositiveAmountStyle(IXLCell cell)
    {
        cell.Style.Font.FontColor = XLColor.FromHtml(SuccessColor);
        cell.Style.Font.Bold      = true;
        cell.Style.NumberFormat.Format = "#,##0.00";
    }

    public static void ApplyNegativeAmountStyle(IXLCell cell)
    {
        cell.Style.Font.FontColor = XLColor.FromHtml(DangerColor);
        cell.Style.Font.Bold      = true;
        cell.Style.NumberFormat.Format = "#,##0.00";
    }

    public static void ApplyAmountFormat(IXLCell cell) =>
        cell.Style.NumberFormat.Format = "#,##0.00";

    private static void ApplyBorder(IXLCell cell)
    {
        var border = XLColor.FromHtml(BorderColor);
        cell.Style.Border.BottomBorder      = XLBorderStyleValues.Thin;
        cell.Style.Border.BottomBorderColor = border;
        cell.Style.Border.RightBorder       = XLBorderStyleValues.Thin;
        cell.Style.Border.RightBorderColor  = border;
    }

    public static string MonthName(int month, string language) => language == "ar"
        ? month switch
        {
            1  => "يناير",  2  => "فبراير",  3  => "مارس",
            4  => "أبريل",  5  => "مايو",    6  => "يونيو",
            7  => "يوليو",  8  => "أغسطس",   9  => "سبتمبر",
            10 => "أكتوبر", 11 => "نوفمبر",  12 => "ديسمبر",
            _  => month.ToString()
        }
        : month switch
        {
            1  => "January",   2  => "February", 3  => "March",
            4  => "April",     5  => "May",       6  => "June",
            7  => "July",      8  => "August",    9  => "September",
            10 => "October",   11 => "November",  12 => "December",
            _  => month.ToString()
        };

    public static byte[] SaveToBytes(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
