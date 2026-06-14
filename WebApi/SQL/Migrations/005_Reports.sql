-- ============================================================
-- Migration 005 — Enterprise Reporting System
-- Tables: MyMoney.ReportTypes, MyMoney.Reports
-- Stored Procedures: 14 SPs (management + data queries)
-- ============================================================

-- ──────────────────────────────────────────────────────────
-- TABLES
-- ──────────────────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReportTypes' AND schema_id = SCHEMA_ID('MyMoney'))
BEGIN
    CREATE TABLE [MyMoney].[ReportTypes] (
        [Id]            TINYINT         NOT NULL IDENTITY(1,1),
        [Key]           NVARCHAR(50)    NOT NULL,
        [NameEn]        NVARCHAR(100)   NOT NULL,
        [NameAr]        NVARCHAR(100)   NOT NULL,
        [DescriptionEn] NVARCHAR(500)   NOT NULL CONSTRAINT [DF_ReportTypes_DescriptionEn] DEFAULT '',
        [DescriptionAr] NVARCHAR(500)   NOT NULL CONSTRAINT [DF_ReportTypes_DescriptionAr] DEFAULT '',
        [SortOrder]     TINYINT         NOT NULL CONSTRAINT [DF_ReportTypes_SortOrder]    DEFAULT 0,
        [IsActive]      BIT             NOT NULL CONSTRAINT [DF_ReportTypes_IsActive]     DEFAULT 1,
        CONSTRAINT [PK_ReportTypes] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [UQ_ReportTypes_Key] UNIQUE ([Key])
    );

    INSERT INTO [MyMoney].[ReportTypes] ([Key], [NameEn], [NameAr], [DescriptionEn], [DescriptionAr], [SortOrder]) VALUES
    ('FinancialSummary',   'Financial Summary',   N'الملخص المالي',       'Monthly overview of income, expenses, and net balance.', N'نظرة شهرية عامة على الدخل والمصروفات وصافي الرصيد.', 1),
    ('TransactionDetail',  'Transaction Detail',  N'تفاصيل المعاملات',   'Complete list of all transactions in the selected period.', N'قائمة كاملة بجميع المعاملات في الفترة المحددة.', 2),
    ('IncomeAnalysis',     'Income Analysis',     N'تحليل الدخل',         'Income breakdown by category with trends and percentages.', N'تحليل الدخل حسب الفئة مع الاتجاهات والنسب المئوية.', 3),
    ('ExpenseAnalysis',    'Expense Analysis',    N'تحليل المصروفات',     'Expense breakdown by category with trends and percentages.', N'تحليل المصروفات حسب الفئة مع الاتجاهات والنسب المئوية.', 4),
    ('CategoryAnalysis',   'Category Analysis',   N'تحليل الفئات',        'Spending patterns and performance metrics by category.', N'أنماط الإنفاق ومقاييس الأداء حسب الفئة.', 5);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Reports' AND schema_id = SCHEMA_ID('MyMoney'))
BEGIN
    CREATE TABLE [MyMoney].[Reports] (
        [Id]             BIGINT          NOT NULL IDENTITY(1,1),
        [UserId]         BIGINT          NOT NULL,
        [ReportTypeId]   TINYINT         NOT NULL,
        [ReportTypeKey]  NVARCHAR(50)    NOT NULL,
        [Status]         TINYINT         NOT NULL CONSTRAINT [DF_Reports_Status]    DEFAULT 1,
        [Language]       NVARCHAR(2)     NOT NULL CONSTRAINT [DF_Reports_Language]  DEFAULT 'en',
        [DateFrom]       DATE            NOT NULL,
        [DateTo]         DATE            NOT NULL,
        [FilePath]       NVARCHAR(500)   NULL,
        [FileSize]       BIGINT          NULL,
        [ErrorMessage]   NVARCHAR(1000)  NULL,
        [RequestedOnUtc] DATETIME2(0)    NOT NULL CONSTRAINT [DF_Reports_RequestedOnUtc] DEFAULT GETUTCDATE(),
        [ProcessedOnUtc] DATETIME2(0)    NULL,
        [CompletedOnUtc] DATETIME2(0)    NULL,
        [ExpiresOnUtc]   DATETIME2(0)    NULL,
        CONSTRAINT [PK_Reports] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_Reports_ReportTypes] FOREIGN KEY ([ReportTypeId]) REFERENCES [MyMoney].[ReportTypes]([Id]),
        CONSTRAINT [FK_Reports_Users] FOREIGN KEY ([UserId]) REFERENCES [MyMoney].[Users]([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_Reports_UserId_Status] ON [MyMoney].[Reports] ([UserId], [Status]);
    CREATE NONCLUSTERED INDEX [IX_Reports_ExpiresOnUtc] ON [MyMoney].[Reports] ([ExpiresOnUtc]) WHERE [ExpiresOnUtc] IS NOT NULL;
END
GO

-- ──────────────────────────────────────────────────────────
-- MANAGEMENT STORED PROCEDURES
-- ──────────────────────────────────────────────────────────

-- usp_Report_GetTypes
IF OBJECT_ID('MyMoney.usp_Report_GetTypes', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_Report_GetTypes];
GO
CREATE PROCEDURE [MyMoney].[usp_Report_GetTypes]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [Key], [NameEn], [NameAr], [DescriptionEn], [DescriptionAr], [SortOrder]
    FROM   [MyMoney].[ReportTypes]
    WHERE  [IsActive] = 1
    ORDER  BY [SortOrder];
END
GO

-- usp_Report_Create
IF OBJECT_ID('MyMoney.usp_Report_Create', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_Report_Create];
GO
CREATE PROCEDURE [MyMoney].[usp_Report_Create]
    @UserId        BIGINT,
    @ReportTypeId  TINYINT,
    @ReportTypeKey NVARCHAR(50),
    @Language      NVARCHAR(2),
    @DateFrom      DATE,
    @DateTo        DATE,
    @ExpiresOnUtc  DATETIME2(0),
    @NewId         BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO [MyMoney].[Reports]
        ([UserId], [ReportTypeId], [ReportTypeKey], [Status], [Language],
         [DateFrom], [DateTo], [RequestedOnUtc], [ExpiresOnUtc])
    VALUES
        (@UserId, @ReportTypeId, @ReportTypeKey, 1, @Language,
         @DateFrom, @DateTo, GETUTCDATE(), @ExpiresOnUtc);
    SET @NewId = SCOPE_IDENTITY();
END
GO

-- usp_Report_UpdateToProcessing
IF OBJECT_ID('MyMoney.usp_Report_UpdateToProcessing', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_Report_UpdateToProcessing];
GO
CREATE PROCEDURE [MyMoney].[usp_Report_UpdateToProcessing]
    @ReportId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [MyMoney].[Reports]
    SET    [Status] = 2, [ProcessedOnUtc] = GETUTCDATE()
    WHERE  [Id] = @ReportId AND [Status] = 1;
END
GO

-- usp_Report_Complete
IF OBJECT_ID('MyMoney.usp_Report_Complete', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_Report_Complete];
GO
CREATE PROCEDURE [MyMoney].[usp_Report_Complete]
    @ReportId BIGINT,
    @FilePath NVARCHAR(500),
    @FileSize BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [MyMoney].[Reports]
    SET    [Status] = 3, [FilePath] = @FilePath, [FileSize] = @FileSize,
           [CompletedOnUtc] = GETUTCDATE()
    WHERE  [Id] = @ReportId;
END
GO

-- usp_Report_Fail
IF OBJECT_ID('MyMoney.usp_Report_Fail', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_Report_Fail];
GO
CREATE PROCEDURE [MyMoney].[usp_Report_Fail]
    @ReportId     BIGINT,
    @ErrorMessage NVARCHAR(1000)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [MyMoney].[Reports]
    SET    [Status] = 4, [ErrorMessage] = @ErrorMessage
    WHERE  [Id] = @ReportId;
END
GO

-- usp_Report_GetById
IF OBJECT_ID('MyMoney.usp_Report_GetById', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_Report_GetById];
GO
CREATE PROCEDURE [MyMoney].[usp_Report_GetById]
    @ReportId BIGINT,
    @UserId   BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.[Id], r.[UserId], r.[ReportTypeId], r.[ReportTypeKey],
           rt.[NameEn] AS [ReportTypeNameEn], rt.[NameAr] AS [ReportTypeNameAr],
           r.[Status], r.[Language], r.[DateFrom], r.[DateTo],
           r.[FilePath], r.[FileSize], r.[ErrorMessage],
           r.[RequestedOnUtc], r.[ProcessedOnUtc], r.[CompletedOnUtc], r.[ExpiresOnUtc]
    FROM   [MyMoney].[Reports] r
    JOIN   [MyMoney].[ReportTypes] rt ON rt.[Id] = r.[ReportTypeId]
    WHERE  r.[Id] = @ReportId AND r.[UserId] = @UserId;
END
GO

-- usp_Report_GetList
IF OBJECT_ID('MyMoney.usp_Report_GetList', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_Report_GetList];
GO
CREATE PROCEDURE [MyMoney].[usp_Report_GetList]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.[Id], r.[UserId], r.[ReportTypeId], r.[ReportTypeKey],
           rt.[NameEn] AS [ReportTypeNameEn], rt.[NameAr] AS [ReportTypeNameAr],
           r.[Status], r.[Language], r.[DateFrom], r.[DateTo],
           r.[FilePath], r.[FileSize], r.[ErrorMessage],
           r.[RequestedOnUtc], r.[ProcessedOnUtc], r.[CompletedOnUtc], r.[ExpiresOnUtc]
    FROM   [MyMoney].[Reports] r
    JOIN   [MyMoney].[ReportTypes] rt ON rt.[Id] = r.[ReportTypeId]
    WHERE  r.[UserId] = @UserId
    ORDER  BY r.[RequestedOnUtc] DESC;
END
GO

-- usp_Report_Delete
IF OBJECT_ID('MyMoney.usp_Report_Delete', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_Report_Delete];
GO
CREATE PROCEDURE [MyMoney].[usp_Report_Delete]
    @ReportId    BIGINT,
    @UserId      BIGINT,
    @RowsDeleted INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM [MyMoney].[Reports]
    WHERE  [Id] = @ReportId AND [UserId] = @UserId;
    SET @RowsDeleted = @@ROWCOUNT;
END
GO

-- usp_Report_ExpireOld
IF OBJECT_ID('MyMoney.usp_Report_ExpireOld', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_Report_ExpireOld];
GO
CREATE PROCEDURE [MyMoney].[usp_Report_ExpireOld]
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [MyMoney].[Reports]
    SET    [Status] = 5
    WHERE  [Status] = 3
      AND  [ExpiresOnUtc] IS NOT NULL
      AND  [ExpiresOnUtc] < GETUTCDATE();
END
GO

-- ──────────────────────────────────────────────────────────
-- DATA QUERY STORED PROCEDURES
-- ──────────────────────────────────────────────────────────

-- usp_Report_FinancialSummary_GetData
IF OBJECT_ID('MyMoney.usp_Report_FinancialSummary_GetData', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_Report_FinancialSummary_GetData];
GO
CREATE PROCEDURE [MyMoney].[usp_Report_FinancialSummary_GetData]
    @UserId   BIGINT,
    @DateFrom DATE,
    @DateTo   DATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Monthly breakdown
    SELECT
        YEAR([TransactionDate])  AS [Year],
        MONTH([TransactionDate]) AS [Month],
        SUM(CASE WHEN tt.[Name] = 'Income'  THEN t.[Amount] ELSE 0 END) AS [Income],
        SUM(CASE WHEN tt.[Name] = 'Expense' THEN t.[Amount] ELSE 0 END) AS [Expenses],
        SUM(CASE WHEN tt.[Name] = 'Income'  THEN t.[Amount] ELSE -t.[Amount] END) AS [Net],
        COUNT(*)                 AS [TransactionCount]
    FROM [MyMoney].[Transactions] t
    JOIN [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    WHERE t.[UserId] = @UserId
      AND t.[TransactionDate] BETWEEN @DateFrom AND @DateTo
    GROUP BY YEAR([TransactionDate]), MONTH([TransactionDate])
    ORDER BY [Year], [Month];

    -- Overall totals
    SELECT
        SUM(CASE WHEN tt.[Name] = 'Income'  THEN t.[Amount] ELSE 0 END) AS [TotalIncome],
        SUM(CASE WHEN tt.[Name] = 'Expense' THEN t.[Amount] ELSE 0 END) AS [TotalExpenses],
        SUM(CASE WHEN tt.[Name] = 'Income'  THEN t.[Amount] ELSE -t.[Amount] END) AS [NetBalance],
        COUNT(*) AS [TotalTransactions],
        COUNT(DISTINCT YEAR([TransactionDate]) * 100 + MONTH([TransactionDate])) AS [ActiveMonths]
    FROM [MyMoney].[Transactions] t
    JOIN [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    WHERE t.[UserId] = @UserId
      AND t.[TransactionDate] BETWEEN @DateFrom AND @DateTo;
END
GO

-- usp_Report_TransactionDetail_GetData
IF OBJECT_ID('MyMoney.usp_Report_TransactionDetail_GetData', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_Report_TransactionDetail_GetData];
GO
CREATE PROCEDURE [MyMoney].[usp_Report_TransactionDetail_GetData]
    @UserId   BIGINT,
    @DateFrom DATE,
    @DateTo   DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        t.[Id]                                                                    AS [TransactionId],
        t.[TransactionDate],
        ISNULL(t.[Description], '')                                               AS [Description],
        c.[NameEn]                                                                AS [CategoryNameEn],
        c.[NameAr]                                                                AS [CategoryNameAr],
        tt.[Name]                                                                 AS [TransactionType],
        t.[Amount],
        ISNULL(t.[Notes], '')                                                     AS [Notes]
    FROM [MyMoney].[Transactions] t
    JOIN [MyMoney].[Categories] c      ON c.[Id]  = t.[CategoryId]
    JOIN [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    WHERE t.[UserId] = @UserId
      AND t.[TransactionDate] BETWEEN @DateFrom AND @DateTo
    ORDER BY t.[TransactionDate] DESC, t.[Id] DESC;

    -- Summary row
    SELECT
        COUNT(*)  AS [TotalCount],
        SUM(CASE WHEN tt.[Name] = 'Income'  THEN t.[Amount] ELSE 0 END) AS [TotalIncome],
        SUM(CASE WHEN tt.[Name] = 'Expense' THEN t.[Amount] ELSE 0 END) AS [TotalExpenses],
        AVG(t.[Amount])                                                   AS [AvgAmount]
    FROM [MyMoney].[Transactions] t
    JOIN [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    WHERE t.[UserId] = @UserId
      AND t.[TransactionDate] BETWEEN @DateFrom AND @DateTo;
END
GO

-- usp_Report_IncomeAnalysis_GetData
IF OBJECT_ID('MyMoney.usp_Report_IncomeAnalysis_GetData', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_Report_IncomeAnalysis_GetData];
GO
CREATE PROCEDURE [MyMoney].[usp_Report_IncomeAnalysis_GetData]
    @UserId   BIGINT,
    @DateFrom DATE,
    @DateTo   DATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TotalIncome DECIMAL(18,2);
    SELECT @TotalIncome = SUM(t.[Amount])
    FROM   [MyMoney].[Transactions] t
    JOIN   [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    WHERE  t.[UserId] = @UserId
      AND  t.[TransactionDate] BETWEEN @DateFrom AND @DateTo
      AND  tt.[Name] = 'Income';

    -- By category
    SELECT
        c.[NameEn]         AS [CategoryNameEn],
        c.[NameAr]         AS [CategoryNameAr],
        SUM(t.[Amount])    AS [TotalAmount],
        COUNT(*)           AS [TransactionCount],
        AVG(t.[Amount])    AS [AvgAmount],
        CASE WHEN ISNULL(@TotalIncome, 0) = 0 THEN 0
             ELSE ROUND(SUM(t.[Amount]) / @TotalIncome * 100, 2) END AS [Percentage]
    FROM [MyMoney].[Transactions] t
    JOIN [MyMoney].[Categories] c        ON c.[Id]  = t.[CategoryId]
    JOIN [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    WHERE t.[UserId] = @UserId
      AND t.[TransactionDate] BETWEEN @DateFrom AND @DateTo
      AND tt.[Name] = 'Income'
    GROUP BY c.[Id], c.[NameEn], c.[NameAr]
    ORDER BY [TotalAmount] DESC;

    -- Monthly trend
    SELECT
        YEAR([TransactionDate])  AS [Year],
        MONTH([TransactionDate]) AS [Month],
        SUM(t.[Amount])          AS [TotalAmount],
        COUNT(*)                 AS [TransactionCount]
    FROM [MyMoney].[Transactions] t
    JOIN [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    WHERE t.[UserId] = @UserId
      AND t.[TransactionDate] BETWEEN @DateFrom AND @DateTo
      AND tt.[Name] = 'Income'
    GROUP BY YEAR([TransactionDate]), MONTH([TransactionDate])
    ORDER BY [Year], [Month];

    -- Totals
    SELECT
        ISNULL(@TotalIncome, 0) AS [TotalIncome],
        COUNT(*)                AS [TotalTransactions]
    FROM [MyMoney].[Transactions] t
    JOIN [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    WHERE t.[UserId] = @UserId
      AND t.[TransactionDate] BETWEEN @DateFrom AND @DateTo
      AND tt.[Name] = 'Income';
END
GO

-- usp_Report_ExpenseAnalysis_GetData
IF OBJECT_ID('MyMoney.usp_Report_ExpenseAnalysis_GetData', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_Report_ExpenseAnalysis_GetData];
GO
CREATE PROCEDURE [MyMoney].[usp_Report_ExpenseAnalysis_GetData]
    @UserId   BIGINT,
    @DateFrom DATE,
    @DateTo   DATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TotalExpenses DECIMAL(18,2);
    SELECT @TotalExpenses = SUM(t.[Amount])
    FROM   [MyMoney].[Transactions] t
    JOIN   [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    WHERE  t.[UserId] = @UserId
      AND  t.[TransactionDate] BETWEEN @DateFrom AND @DateTo
      AND  tt.[Name] = 'Expense';

    -- By category
    SELECT
        c.[NameEn]         AS [CategoryNameEn],
        c.[NameAr]         AS [CategoryNameAr],
        SUM(t.[Amount])    AS [TotalAmount],
        COUNT(*)           AS [TransactionCount],
        AVG(t.[Amount])    AS [AvgAmount],
        CASE WHEN ISNULL(@TotalExpenses, 0) = 0 THEN 0
             ELSE ROUND(SUM(t.[Amount]) / @TotalExpenses * 100, 2) END AS [Percentage]
    FROM [MyMoney].[Transactions] t
    JOIN [MyMoney].[Categories] c        ON c.[Id]  = t.[CategoryId]
    JOIN [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    WHERE t.[UserId] = @UserId
      AND t.[TransactionDate] BETWEEN @DateFrom AND @DateTo
      AND tt.[Name] = 'Expense'
    GROUP BY c.[Id], c.[NameEn], c.[NameAr]
    ORDER BY [TotalAmount] DESC;

    -- Monthly trend
    SELECT
        YEAR([TransactionDate])  AS [Year],
        MONTH([TransactionDate]) AS [Month],
        SUM(t.[Amount])          AS [TotalAmount],
        COUNT(*)                 AS [TransactionCount]
    FROM [MyMoney].[Transactions] t
    JOIN [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    WHERE t.[UserId] = @UserId
      AND t.[TransactionDate] BETWEEN @DateFrom AND @DateTo
      AND tt.[Name] = 'Expense'
    GROUP BY YEAR([TransactionDate]), MONTH([TransactionDate])
    ORDER BY [Year], [Month];

    -- Totals
    SELECT
        ISNULL(@TotalExpenses, 0) AS [TotalExpenses],
        COUNT(*)                  AS [TotalTransactions]
    FROM [MyMoney].[Transactions] t
    JOIN [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    WHERE t.[UserId] = @UserId
      AND t.[TransactionDate] BETWEEN @DateFrom AND @DateTo
      AND tt.[Name] = 'Expense';
END
GO

-- usp_Report_CategoryAnalysis_GetData
IF OBJECT_ID('MyMoney.usp_Report_CategoryAnalysis_GetData', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_Report_CategoryAnalysis_GetData];
GO
CREATE PROCEDURE [MyMoney].[usp_Report_CategoryAnalysis_GetData]
    @UserId   BIGINT,
    @DateFrom DATE,
    @DateTo   DATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Category performance (all types)
    SELECT
        c.[NameEn]           AS [CategoryNameEn],
        c.[NameAr]           AS [CategoryNameAr],
        tt.[Name]            AS [TransactionType],
        SUM(t.[Amount])      AS [TotalAmount],
        COUNT(*)             AS [TransactionCount],
        AVG(t.[Amount])      AS [AvgAmount],
        MAX(t.[Amount])      AS [MaxAmount],
        MIN(t.[Amount])      AS [MinAmount]
    FROM [MyMoney].[Transactions] t
    JOIN [MyMoney].[Categories] c        ON c.[Id]  = t.[CategoryId]
    JOIN [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    WHERE t.[UserId] = @UserId
      AND t.[TransactionDate] BETWEEN @DateFrom AND @DateTo
    GROUP BY c.[Id], c.[NameEn], c.[NameAr], tt.[Id], tt.[Name]
    ORDER BY [TotalAmount] DESC;

    -- Overall summary
    SELECT
        COUNT(DISTINCT t.[CategoryId])  AS [UniqueCategories],
        COUNT(*)                        AS [TotalTransactions],
        SUM(CASE WHEN tt.[Name] = 'Income'  THEN t.[Amount] ELSE 0 END) AS [TotalIncome],
        SUM(CASE WHEN tt.[Name] = 'Expense' THEN t.[Amount] ELSE 0 END) AS [TotalExpenses]
    FROM [MyMoney].[Transactions] t
    JOIN [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    WHERE t.[UserId] = @UserId
      AND t.[TransactionDate] BETWEEN @DateFrom AND @DateTo;
END
GO
