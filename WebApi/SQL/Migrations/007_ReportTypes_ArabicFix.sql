-- Migration 007 — Fix garbled Arabic text in ReportTypes (sqlcmd encoding fix)
UPDATE [MyMoney].[ReportTypes] SET
    [NameAr]        = N'الملخص المالي',
    [DescriptionAr] = N'نظرة شهرية عامة على الدخل والمصروفات وصافي الرصيد.'
WHERE [Key] = 'FinancialSummary';

UPDATE [MyMoney].[ReportTypes] SET
    [NameAr]        = N'تفاصيل المعاملات',
    [DescriptionAr] = N'قائمة كاملة بجميع المعاملات في الفترة المحددة.'
WHERE [Key] = 'TransactionDetail';

UPDATE [MyMoney].[ReportTypes] SET
    [NameAr]        = N'تحليل الدخل',
    [DescriptionAr] = N'تحليل الدخل حسب الفئة مع الاتجاهات والنسب المئوية.'
WHERE [Key] = 'IncomeAnalysis';

UPDATE [MyMoney].[ReportTypes] SET
    [NameAr]        = N'تحليل المصروفات',
    [DescriptionAr] = N'تحليل المصروفات حسب الفئة مع الاتجاهات والنسب المئوية.'
WHERE [Key] = 'ExpenseAnalysis';

UPDATE [MyMoney].[ReportTypes] SET
    [NameAr]        = N'تحليل الفئات',
    [DescriptionAr] = N'أنماط الإنفاق ومقاييس الأداء حسب الفئة.'
WHERE [Key] = 'CategoryAnalysis';
