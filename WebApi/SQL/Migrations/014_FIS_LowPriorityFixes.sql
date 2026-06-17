-- =============================================================================
-- Migration: 014_FIS_LowPriorityFixes
-- Description: Resolves Low-severity findings from the FIS audit.
--
--   L6 — Missing "Mark All Insights as Read" endpoint
--        CREATE: usp_FIL_Insight_MarkAllRead
--
--   L9 — Correlated subquery for TopCategoryId in usp_FIL_Snapshot_Compute
--        ALTER:  usp_FIL_Snapshot_Compute — replace subquery with a
--                pre-aggregated variable computed once before the main SELECT
--
-- Author: Laith Al-Shamasneh
-- Date:   2026-06-17
-- =============================================================================

SET QUOTED_IDENTIFIER ON;
GO
SET ANSI_NULLS ON;
GO


-- =============================================================================
-- L6 — Create: usp_FIL_Insight_MarkAllRead
-- =============================================================================
-- Marks every unread insight for a user as read in a single UPDATE.
-- @RowsAffected is an OUTPUT parameter so the service can report the count
-- (kept for parity with the single-read SP; caller can ignore the value).
-- =============================================================================

IF OBJECT_ID('MyMoney.usp_FIL_Insight_MarkAllRead', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_Insight_MarkAllRead];
GO

CREATE PROCEDURE [MyMoney].[usp_FIL_Insight_MarkAllRead]
    @UserId       BIGINT,
    @RowsAffected INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [MyMoney].[FinancialInsights]
    SET    [IsRead]    = 1,
           [ReadAtUtc] = GETUTCDATE()
    WHERE  [UserId]    = @UserId
      AND  [IsRead]    = 0;

    SET @RowsAffected = @@ROWCOUNT;
END
GO


-- =============================================================================
-- L9 — Fix: usp_FIL_Snapshot_Compute — eliminate correlated subquery
-- =============================================================================
-- The TopCategoryId correlated subquery in the original procedure re-scans the
-- Transactions table for every row produced by the outer SELECT. Although the
-- outer SELECT produces exactly one row (an aggregate), the query plan still
-- treats it as a correlated subquery and blocks hash/merge join strategies.
--
-- Fix: pre-aggregate the top category into a local variable before the main
-- SELECT. The rest of the procedure body is identical to migration 013.
-- =============================================================================

ALTER PROCEDURE [MyMoney].[usp_FIL_Snapshot_Compute]
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

    -- For the current in-progress month use elapsed days; for completed months
    -- use the full month length (AverageDailySpend denominator fix from H5).
    DECLARE @ElapsedDays INT = CASE
        WHEN @Year  = YEAR(@Today)
         AND @Month = MONTH(@Today) THEN ISNULL(NULLIF(DAY(@Today), 0), 1)
        ELSE @DaysInMonth
    END;

    -- L9 fix: compute the top spending category once into a variable so the
    -- main SELECT does not embed a correlated subquery.
    DECLARE @TopCategoryId INT = NULL;

    SELECT TOP 1 @TopCategoryId = t.[CategoryId]
    FROM   [MyMoney].[Transactions]     t
    JOIN   [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    WHERE  t.[UserId]            = @UserId
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
      AND t.[TransactionDate]  >= @PeriodStart
      AND t.[TransactionDate]   < @PeriodEnd;
END
GO
