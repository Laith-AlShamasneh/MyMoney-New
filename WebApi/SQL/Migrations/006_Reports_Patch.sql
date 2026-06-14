-- ============================================================
-- Migration 006 — Reports Patch
-- Fixes from 005: correct FK column (UserId), no TransactionTypes table,
-- correct PK column names (CategoryId, TransactionId)
-- TransactionTypeId: 1 = Income, 2 = Expense
-- ============================================================

-- ──────────────────────────────────────────────────────────
-- CREATE Reports TABLE (failed in 005 due to wrong FK column)
-- ──────────────────────────────────────────────────────────

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
        CONSTRAINT [FK_Reports_Users] FOREIGN KEY ([UserId]) REFERENCES [MyMoney].[Users]([UserId]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_Reports_UserId_Status] ON [MyMoney].[Reports] ([UserId], [Status]);
    CREATE NONCLUSTERED INDEX [IX_Reports_ExpiresOnUtc]  ON [MyMoney].[Reports] ([ExpiresOnUtc]) WHERE [ExpiresOnUtc] IS NOT NULL;
END
GO

-- ──────────────────────────────────────────────────────────
-- FIX DATA QUERY STORED PROCEDURES
-- (No TransactionTypes table; use TransactionTypeId directly: 1=Income, 2=Expense)
-- ──────────────────────────────────────────────────────────

-- usp_Report_FinancialSummary_GetData (recreate — references non-existent TransactionTypes table)
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
        SUM(CASE WHEN [TransactionTypeId] = 1 THEN [Amount] ELSE 0 END) AS [Income],
        SUM(CASE WHEN [TransactionTypeId] = 2 THEN [Amount] ELSE 0 END) AS [Expenses],
        SUM(CASE WHEN [TransactionTypeId] = 1 THEN [Amount] ELSE -[Amount] END) AS [Net],
        COUNT(*) AS [TransactionCount]
    FROM  [MyMoney].[Transactions]
    WHERE [UserId] = @UserId
      AND [IsActive] = 1
      AND [TransactionDate] BETWEEN @DateFrom AND @DateTo
    GROUP BY YEAR([TransactionDate]), MONTH([TransactionDate])
    ORDER BY [Year], [Month];

    -- Overall totals
    SELECT
        SUM(CASE WHEN [TransactionTypeId] = 1 THEN [Amount] ELSE 0 END) AS [TotalIncome],
        SUM(CASE WHEN [TransactionTypeId] = 2 THEN [Amount] ELSE 0 END) AS [TotalExpenses],
        SUM(CASE WHEN [TransactionTypeId] = 1 THEN [Amount] ELSE -[Amount] END) AS [NetBalance],
        COUNT(*) AS [TotalTransactions],
        COUNT(DISTINCT YEAR([TransactionDate]) * 100 + MONTH([TransactionDate])) AS [ActiveMonths]
    FROM  [MyMoney].[Transactions]
    WHERE [UserId] = @UserId
      AND [IsActive] = 1
      AND [TransactionDate] BETWEEN @DateFrom AND @DateTo;
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
        t.[TransactionId],
        t.[TransactionDate],
        ISNULL(t.[Description], '')                                                   AS [Description],
        c.[NameEn]                                                                     AS [CategoryNameEn],
        c.[NameAr]                                                                     AS [CategoryNameAr],
        CASE t.[TransactionTypeId] WHEN 1 THEN 'Income' WHEN 2 THEN 'Expense' ELSE 'Unknown' END AS [TransactionType],
        t.[Amount],
        ISNULL(t.[Notes], '')                                                          AS [Notes]
    FROM  [MyMoney].[Transactions] t
    JOIN  [MyMoney].[Categories]   c ON c.[CategoryId] = t.[CategoryId]
    WHERE t.[UserId] = @UserId
      AND t.[IsActive] = 1
      AND t.[TransactionDate] BETWEEN @DateFrom AND @DateTo
    ORDER BY t.[TransactionDate] DESC, t.[TransactionId] DESC;

    -- Summary row
    SELECT
        COUNT(*)                                                                       AS [TotalCount],
        SUM(CASE WHEN [TransactionTypeId] = 1 THEN [Amount] ELSE 0 END)               AS [TotalIncome],
        SUM(CASE WHEN [TransactionTypeId] = 2 THEN [Amount] ELSE 0 END)               AS [TotalExpenses],
        AVG([Amount])                                                                  AS [AvgAmount]
    FROM  [MyMoney].[Transactions]
    WHERE [UserId] = @UserId
      AND [IsActive] = 1
      AND [TransactionDate] BETWEEN @DateFrom AND @DateTo;
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
    SELECT @TotalIncome = SUM([Amount])
    FROM   [MyMoney].[Transactions]
    WHERE  [UserId] = @UserId
      AND  [IsActive] = 1
      AND  [TransactionDate] BETWEEN @DateFrom AND @DateTo
      AND  [TransactionTypeId] = 1;

    -- By category
    SELECT
        c.[NameEn]          AS [CategoryNameEn],
        c.[NameAr]          AS [CategoryNameAr],
        SUM(t.[Amount])     AS [TotalAmount],
        COUNT(*)            AS [TransactionCount],
        AVG(t.[Amount])     AS [AvgAmount],
        CASE WHEN ISNULL(@TotalIncome, 0) = 0 THEN 0
             ELSE ROUND(SUM(t.[Amount]) / @TotalIncome * 100, 2) END AS [Percentage]
    FROM  [MyMoney].[Transactions] t
    JOIN  [MyMoney].[Categories]   c ON c.[CategoryId] = t.[CategoryId]
    WHERE t.[UserId] = @UserId
      AND t.[IsActive] = 1
      AND t.[TransactionDate] BETWEEN @DateFrom AND @DateTo
      AND t.[TransactionTypeId] = 1
    GROUP BY c.[CategoryId], c.[NameEn], c.[NameAr]
    ORDER BY [TotalAmount] DESC;

    -- Monthly trend
    SELECT
        YEAR([TransactionDate])  AS [Year],
        MONTH([TransactionDate]) AS [Month],
        SUM([Amount])            AS [TotalAmount],
        COUNT(*)                 AS [TransactionCount]
    FROM  [MyMoney].[Transactions]
    WHERE [UserId] = @UserId
      AND [IsActive] = 1
      AND [TransactionDate] BETWEEN @DateFrom AND @DateTo
      AND [TransactionTypeId] = 1
    GROUP BY YEAR([TransactionDate]), MONTH([TransactionDate])
    ORDER BY [Year], [Month];

    -- Totals
    SELECT
        ISNULL(@TotalIncome, 0) AS [TotalIncome],
        COUNT(*)                AS [TotalTransactions]
    FROM  [MyMoney].[Transactions]
    WHERE [UserId] = @UserId
      AND [IsActive] = 1
      AND [TransactionDate] BETWEEN @DateFrom AND @DateTo
      AND [TransactionTypeId] = 1;
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
    SELECT @TotalExpenses = SUM([Amount])
    FROM   [MyMoney].[Transactions]
    WHERE  [UserId] = @UserId
      AND  [IsActive] = 1
      AND  [TransactionDate] BETWEEN @DateFrom AND @DateTo
      AND  [TransactionTypeId] = 2;

    -- By category
    SELECT
        c.[NameEn]          AS [CategoryNameEn],
        c.[NameAr]          AS [CategoryNameAr],
        SUM(t.[Amount])     AS [TotalAmount],
        COUNT(*)            AS [TransactionCount],
        AVG(t.[Amount])     AS [AvgAmount],
        CASE WHEN ISNULL(@TotalExpenses, 0) = 0 THEN 0
             ELSE ROUND(SUM(t.[Amount]) / @TotalExpenses * 100, 2) END AS [Percentage]
    FROM  [MyMoney].[Transactions] t
    JOIN  [MyMoney].[Categories]   c ON c.[CategoryId] = t.[CategoryId]
    WHERE t.[UserId] = @UserId
      AND t.[IsActive] = 1
      AND t.[TransactionDate] BETWEEN @DateFrom AND @DateTo
      AND t.[TransactionTypeId] = 2
    GROUP BY c.[CategoryId], c.[NameEn], c.[NameAr]
    ORDER BY [TotalAmount] DESC;

    -- Monthly trend
    SELECT
        YEAR([TransactionDate])  AS [Year],
        MONTH([TransactionDate]) AS [Month],
        SUM([Amount])            AS [TotalAmount],
        COUNT(*)                 AS [TransactionCount]
    FROM  [MyMoney].[Transactions]
    WHERE [UserId] = @UserId
      AND [IsActive] = 1
      AND [TransactionDate] BETWEEN @DateFrom AND @DateTo
      AND [TransactionTypeId] = 2
    GROUP BY YEAR([TransactionDate]), MONTH([TransactionDate])
    ORDER BY [Year], [Month];

    -- Totals
    SELECT
        ISNULL(@TotalExpenses, 0) AS [TotalExpenses],
        COUNT(*)                  AS [TotalTransactions]
    FROM  [MyMoney].[Transactions]
    WHERE [UserId] = @UserId
      AND [IsActive] = 1
      AND [TransactionDate] BETWEEN @DateFrom AND @DateTo
      AND [TransactionTypeId] = 2;
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
        c.[NameEn]      AS [CategoryNameEn],
        c.[NameAr]      AS [CategoryNameAr],
        CASE t.[TransactionTypeId] WHEN 1 THEN 'Income' WHEN 2 THEN 'Expense' ELSE 'Unknown' END AS [TransactionType],
        SUM(t.[Amount]) AS [TotalAmount],
        COUNT(*)        AS [TransactionCount],
        AVG(t.[Amount]) AS [AvgAmount],
        MAX(t.[Amount]) AS [MaxAmount],
        MIN(t.[Amount]) AS [MinAmount]
    FROM  [MyMoney].[Transactions] t
    JOIN  [MyMoney].[Categories]   c ON c.[CategoryId] = t.[CategoryId]
    WHERE t.[UserId] = @UserId
      AND t.[IsActive] = 1
      AND t.[TransactionDate] BETWEEN @DateFrom AND @DateTo
    GROUP BY c.[CategoryId], c.[NameEn], c.[NameAr], t.[TransactionTypeId]
    ORDER BY [TotalAmount] DESC;

    -- Overall summary
    SELECT
        COUNT(DISTINCT [CategoryId])                                                  AS [UniqueCategories],
        COUNT(*)                                                                      AS [TotalTransactions],
        SUM(CASE WHEN [TransactionTypeId] = 1 THEN [Amount] ELSE 0 END)              AS [TotalIncome],
        SUM(CASE WHEN [TransactionTypeId] = 2 THEN [Amount] ELSE 0 END)              AS [TotalExpenses]
    FROM  [MyMoney].[Transactions]
    WHERE [UserId] = @UserId
      AND [IsActive] = 1
      AND [TransactionDate] BETWEEN @DateFrom AND @DateTo;
END
GO
