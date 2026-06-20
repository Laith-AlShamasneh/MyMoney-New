-- =============================================================================
-- FIS Consistency Fix
-- Description : Fixes three categories of data-consistency bugs in the
--               Financial Intelligence System:
--
--   1. Add AND t.IsActive = 1 to every FIS stored procedure that reads from
--      Transactions — ensures soft-deleted rows are excluded from all FIS
--      computations (snapshots, category analytics, anomaly detection).
--
--   2. Add @OldTransactionDate DATE OUTPUT to usp_Transaction_Update so the
--      application layer can detect month-boundary date changes and enqueue
--      snapshot recomputation for both affected months.
--
--   3. Add @DeletedDate DATE OUTPUT to usp_Transaction_Delete so the
--      application layer knows which month to recompute after a soft-delete.
--
-- Run order : Execute the entire script in a single SSMS batch.
-- Safe to re-run : All statements use CREATE OR ALTER PROCEDURE.
-- =============================================================================

USE [MyMoney];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO


-- ─────────────────────────────────────────────────────────────────────────────
-- 1. usp_FIL_Snapshot_Compute  — add AND t.IsActive = 1 (2 places)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_FIL_Snapshot_Compute]
    @UserId BIGINT,
    @Year   INT,
    @Month  INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Today       DATE = CAST(GETUTCDATE() AS DATE);
    DECLARE @PeriodStart DATE = DATEFROMPARTS(@Year, @Month, 1);
    DECLARE @PeriodEnd   DATE = DATEADD(MONTH, 1, @PeriodStart);
    DECLARE @DaysInMonth INT  = DAY(EOMONTH(@PeriodStart));

    DECLARE @ElapsedDays INT = CASE
        WHEN @Year  = YEAR(@Today)
         AND @Month = MONTH(@Today) THEN ISNULL(NULLIF(DAY(@Today), 0), 1)
        ELSE @DaysInMonth
    END;

    -- Pre-compute the top spending category (L9 correlated-subquery fix).
    DECLARE @TopCategoryId INT = NULL;

    SELECT TOP 1 @TopCategoryId = t.[CategoryId]
    FROM   [MyMoney].[Transactions]     t
    JOIN   [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    WHERE  t.[UserId]            = @UserId
      AND  t.[IsActive]          = 1                  -- FIS fix: exclude soft-deleted rows
      AND  tt.[Name]             = 'Expense'
      AND  t.[TransactionDate]  >= @PeriodStart
      AND  t.[TransactionDate]   < @PeriodEnd
    GROUP  BY t.[CategoryId]
    ORDER  BY SUM(t.[Amount]) DESC;

    SELECT
        ISNULL(SUM(CASE WHEN tt.[Name] = 'Income'  THEN t.[Amount] ELSE 0 END), 0)   AS TotalIncome,
        ISNULL(SUM(CASE WHEN tt.[Name] = 'Expense' THEN t.[Amount] ELSE 0 END), 0)   AS TotalExpense,
        ISNULL(SUM(CASE
            WHEN tt.[Name] = 'Income'  THEN  t.[Amount]
            WHEN tt.[Name] = 'Expense' THEN -t.[Amount]
            ELSE 0
        END), 0)                                                                      AS NetBalance,
        ISNULL(
            SUM(CASE WHEN tt.[Name] = 'Expense' THEN t.[Amount] ELSE 0 END)
            / NULLIF(@ElapsedDays, 0),
        0)                                                                            AS AverageDailySpend,
        ISNULL(AVG(t.[Amount]), 0)                                                    AS AverageTransactionValue,
        COUNT(*)                                                                      AS TransactionCount,
        @TopCategoryId                                                                AS TopCategoryId
    FROM  [MyMoney].[Transactions]     t
    JOIN  [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    WHERE t.[UserId]            = @UserId
      AND t.[IsActive]          = 1                  -- FIS fix: exclude soft-deleted rows
      AND t.[TransactionDate]  >= @PeriodStart
      AND t.[TransactionDate]   < @PeriodEnd;
END
GO


-- ─────────────────────────────────────────────────────────────────────────────
-- 2. usp_FIL_CategoryAnalytics_Compute  — add AND t.IsActive = 1 (3 places)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_FIL_CategoryAnalytics_Compute]
    @UserId BIGINT,
    @Year   INT,
    @Month  INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PrevYear  INT = CASE WHEN @Month = 1 THEN @Year - 1 ELSE @Year  END;
    DECLARE @PrevMonth INT = CASE WHEN @Month = 1 THEN 12        ELSE @Month - 1 END;

    DECLARE @PeriodStart     DATE = DATEFROMPARTS(@Year,     @Month,     1);
    DECLARE @PeriodEnd       DATE = DATEADD(MONTH, 1, @PeriodStart);
    DECLARE @PrevPeriodStart DATE = DATEFROMPARTS(@PrevYear, @PrevMonth, 1);
    DECLARE @PrevPeriodEnd   DATE = DATEADD(MONTH, 1, @PrevPeriodStart);

    -- Place 1: total expenses denominator
    DECLARE @TotalExpenses DECIMAL(18,2);
    SELECT  @TotalExpenses = SUM(t.[Amount])
    FROM    [MyMoney].[Transactions] t
    JOIN    [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    WHERE   t.[UserId]          = @UserId
      AND   t.[IsActive]        = 1                  -- FIS fix: exclude soft-deleted rows
      AND   t.[TransactionDate] >= @PeriodStart
      AND   t.[TransactionDate] <  @PeriodEnd
      AND   tt.[Name] = 'Expense';

    SELECT
        0                                                             AS Id,
        c.[CategoryId]                                                AS CategoryId,
        c.[NameEn]                                                    AS CategoryNameEn,
        c.[NameAr]                                                    AS CategoryNameAr,
        SUM(t.[Amount])                                               AS TotalSpent,
        COUNT(t.[TransactionId])                                      AS TransactionCount,
        AVG(t.[Amount])                                               AS AverageSpent,
        ROUND(SUM(t.[Amount]) / NULLIF(@TotalExpenses, 0) * 100, 2)  AS PercentageOfTotal,
        CAST(CASE
            WHEN ISNULL(prev.[PrevTotal], 0) = 0 THEN 1
            WHEN SUM(t.[Amount]) > prev.[PrevTotal] * 1.05 THEN 2
            WHEN SUM(t.[Amount]) < prev.[PrevTotal] * 0.95 THEN 3
            ELSE 1
        END AS TINYINT)                                               AS TrendDirection,
        ISNULL(prev.[PrevTotal], 0)                                   AS PreviousPeriodTotal,
        CASE
            WHEN ISNULL(prev.[PrevTotal], 0) = 0 THEN 0.00
            ELSE ROUND((SUM(t.[Amount]) - prev.[PrevTotal]) / prev.[PrevTotal] * 100, 2)
        END                                                           AS ChangePercentage,
        @PeriodStart                                                  AS PeriodStart,
        EOMONTH(@PeriodStart)                                         AS PeriodEnd
    -- Place 2: current-period query
    FROM [MyMoney].[Transactions] t
    JOIN [MyMoney].[Categories] c        ON c.[CategoryId]  = t.[CategoryId]
    JOIN [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    LEFT JOIN (
        -- Place 3: previous-period subquery
        SELECT tp.[CategoryId], SUM(tp.[Amount]) AS PrevTotal
        FROM   [MyMoney].[Transactions] tp
        JOIN   [MyMoney].[TransactionTypes] ttp ON ttp.[Id] = tp.[TransactionTypeId]
        WHERE  tp.[UserId]          = @UserId
          AND  tp.[IsActive]        = 1              -- FIS fix: exclude soft-deleted rows
          AND  tp.[TransactionDate] >= @PrevPeriodStart
          AND  tp.[TransactionDate] <  @PrevPeriodEnd
          AND  ttp.[Name] = 'Expense'
        GROUP  BY tp.[CategoryId]
    ) prev ON prev.[CategoryId] = t.[CategoryId]
    WHERE t.[UserId]          = @UserId
      AND t.[IsActive]        = 1                    -- FIS fix: exclude soft-deleted rows
      AND t.[TransactionDate] >= @PeriodStart
      AND t.[TransactionDate] <  @PeriodEnd
      AND tt.[Name] = 'Expense'
    GROUP BY c.[CategoryId], c.[NameEn], c.[NameAr], prev.[PrevTotal];
END
GO


-- ─────────────────────────────────────────────────────────────────────────────
-- 3. usp_FIL_Transaction_GetLargeRecent  — add AND t.IsActive = 1 (2 places)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_FIL_Transaction_GetLargeRecent]
    @FromUtc     DATETIME2(0),
    @AbsoluteMin DECIMAL(18,3) = 1000
AS
BEGIN
    SET NOCOUNT ON;

    WITH UserAverages AS (
        SELECT
            t.[UserId],
            AVG(t.[Amount]) AS UserAverage
        FROM  [MyMoney].[Transactions]     t
        JOIN  [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
        WHERE tt.[Name]       = 'Expense'
          AND t.[IsActive]    = 1                    -- FIS fix: exclude soft-deleted rows
          AND t.[CreatedAt] >= DATEADD(MONTH, -3, GETUTCDATE())
          AND t.[CreatedAt] <  @FromUtc
        GROUP BY t.[UserId]
    )
    SELECT
        t.[UserId],
        t.[TransactionId]           AS TransactionId,
        t.[Amount],
        ISNULL(ua.[UserAverage], 0) AS UserAverage,
        c.[NameEn]                  AS CategoryNameEn,
        c.[NameAr]                  AS CategoryNameAr
    FROM  [MyMoney].[Transactions]     t
    JOIN  [MyMoney].[TransactionTypes] tt ON tt.[Id]        = t.[TransactionTypeId]
    JOIN  [MyMoney].[Categories]        c ON  c.[CategoryId] = t.[CategoryId]
    LEFT JOIN UserAverages             ua ON ua.[UserId]     = t.[UserId]
    WHERE tt.[Name]       = 'Expense'
      AND t.[IsActive]    = 1                        -- FIS fix: exclude soft-deleted rows
      AND t.[CreatedAt] >= @FromUtc
      AND (
               (ua.[UserAverage] IS NOT NULL AND ua.[UserAverage] > 0 AND t.[Amount] > ua.[UserAverage] * 2)
            OR (ua.[UserAverage] IS NULL     AND t.[Amount] >= @AbsoluteMin)
          );
END
GO


-- ─────────────────────────────────────────────────────────────────────────────
-- 4. usp_Transaction_Update  — add @OldTransactionDate DATE OUTPUT
--    Captures the existing TransactionDate before applying the update so the
--    application can enqueue snapshot recomputation for both months when the
--    date crosses a month boundary.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Transaction_Update]
    @UserId              BIGINT,
    @TransactionId       BIGINT,
    @CategoryId          INT,
    @TransactionTypeId   TINYINT,
    @Amount              DECIMAL(18,2),
    @Description         NVARCHAR(500)  = NULL,
    @TransactionDate     DATE,
    @Notes               NVARCHAR(1000) = NULL,
    @AffectedRows        INT            OUTPUT,
    @OldTransactionDate  DATE           OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @OldTransactionDate = NULL;

    -- Validate category/type pairing before touching the row.
    IF NOT EXISTS (
        SELECT 1 FROM [MyMoney].[Categories]
        WHERE  CategoryId        = @CategoryId
          AND  TransactionTypeId = @TransactionTypeId
          AND  IsActive          = 1
    )
    BEGIN
        SET @AffectedRows = 0;
        RETURN;
    END

    -- Capture the current date before overwriting it.
    SELECT @OldTransactionDate = [TransactionDate]
    FROM   [MyMoney].[Transactions]
    WHERE  TransactionId = @TransactionId
      AND  UserId        = @UserId
      AND  IsActive      = 1;

    IF @OldTransactionDate IS NULL
    BEGIN
        SET @AffectedRows = 0;
        RETURN;
    END

    UPDATE [MyMoney].[Transactions]
    SET
        CategoryId        = @CategoryId,
        TransactionTypeId = @TransactionTypeId,
        Amount            = @Amount,
        Description       = @Description,
        TransactionDate   = @TransactionDate,
        Notes             = @Notes,
        UpdatedAt         = GETUTCDATE(),
        UpdatedBy         = @UserId
    WHERE TransactionId = @TransactionId
      AND UserId        = @UserId
      AND IsActive      = 1;

    SET @AffectedRows = @@ROWCOUNT;
END
GO


-- ─────────────────────────────────────────────────────────────────────────────
-- 5. usp_Transaction_Delete  — add @DeletedDate DATE OUTPUT
--    Returns the TransactionDate of the soft-deleted row so the application
--    can enqueue snapshot recomputation for the correct month.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Transaction_Delete]
    @UserId        BIGINT,
    @TransactionId BIGINT,
    @AffectedRows  INT  OUTPUT,
    @DeletedDate   DATE OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @DeletedDate = NULL;

    -- Capture the date before soft-deleting.
    SELECT @DeletedDate = [TransactionDate]
    FROM   [MyMoney].[Transactions]
    WHERE  TransactionId = @TransactionId
      AND  UserId        = @UserId
      AND  IsActive      = 1;

    IF @DeletedDate IS NULL
    BEGIN
        SET @AffectedRows = 0;
        RETURN;
    END

    UPDATE [MyMoney].[Transactions]
    SET IsActive  = 0,
        UpdatedAt = GETUTCDATE(),
        UpdatedBy = @UserId
    WHERE TransactionId = @TransactionId
      AND UserId        = @UserId
      AND IsActive      = 1;

    SET @AffectedRows = @@ROWCOUNT;
END
GO
