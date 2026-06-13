-- ============================================================
-- Stored Procedure: MyMoney.usp_Dashboard_GetSummary
-- Description: Returns 4 result sets for the dashboard in a
--              single round-trip:
--                1. KPI summary (current month + previous month)
--                2. 6-month income/expense trend
--                3. Expense category breakdown (current month)
--                4. 10 most recent transactions
-- ============================================================

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Dashboard_GetSummary]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Today          DATE = CAST(GETUTCDATE() AS DATE);
    DECLARE @MonthStart     DATE = DATEFROMPARTS(YEAR(@Today), MONTH(@Today), 1);
    DECLARE @NextMonthStart DATE = DATEADD(MONTH,  1, @MonthStart);
    DECLARE @LastMonthStart DATE = DATEADD(MONTH, -1, @MonthStart);
    DECLARE @SixMonthsAgo   DATE = DATEADD(MONTH, -5, @MonthStart);

    -- ── Result Set 1: KPI summary ────────────────────────────────────────────
    SELECT
        ISNULL(SUM(CASE WHEN TransactionTypeId = 1
                         AND TransactionDate >= @MonthStart
                         AND TransactionDate <  @NextMonthStart
                    THEN Amount ELSE 0 END), 0)  AS CurrentIncome,

        ISNULL(SUM(CASE WHEN TransactionTypeId = 2
                         AND TransactionDate >= @MonthStart
                         AND TransactionDate <  @NextMonthStart
                    THEN Amount ELSE 0 END), 0)  AS CurrentExpenses,

        COUNT(CASE WHEN TransactionDate >= @MonthStart
                    AND TransactionDate <  @NextMonthStart
               THEN 1 END)                        AS CurrentTransactionCount,

        ISNULL(SUM(CASE WHEN TransactionTypeId = 1
                         AND TransactionDate >= @LastMonthStart
                         AND TransactionDate <  @MonthStart
                    THEN Amount ELSE 0 END), 0)  AS PreviousIncome,

        ISNULL(SUM(CASE WHEN TransactionTypeId = 2
                         AND TransactionDate >= @LastMonthStart
                         AND TransactionDate <  @MonthStart
                    THEN Amount ELSE 0 END), 0)  AS PreviousExpenses,

        COUNT(CASE WHEN TransactionDate >= @LastMonthStart
                    AND TransactionDate <  @MonthStart
               THEN 1 END)                        AS PreviousTransactionCount
    FROM MyMoney.Transactions
    WHERE UserId   = @UserId
      AND IsActive = 1
      AND TransactionDate >= @LastMonthStart
      AND TransactionDate <  @NextMonthStart;

    -- ── Result Set 2: 6-Month trend ──────────────────────────────────────────
    SELECT
        YEAR(TransactionDate)  AS [Year],
        MONTH(TransactionDate) AS [Month],
        ISNULL(SUM(CASE WHEN TransactionTypeId = 1 THEN Amount ELSE 0 END), 0) AS Income,
        ISNULL(SUM(CASE WHEN TransactionTypeId = 2 THEN Amount ELSE 0 END), 0) AS Expenses
    FROM MyMoney.Transactions
    WHERE UserId   = @UserId
      AND IsActive = 1
      AND TransactionDate >= @SixMonthsAgo
      AND TransactionDate <  @NextMonthStart
    GROUP BY YEAR(TransactionDate), MONTH(TransactionDate)
    ORDER BY YEAR(TransactionDate), MONTH(TransactionDate);

    -- ── Result Set 3: Expense category breakdown (current month) ─────────────
    SELECT
        c.CategoryId,
        c.NameEn,
        c.NameAr,
        ISNULL(SUM(t.Amount), 0) AS TotalAmount
    FROM MyMoney.Categories c
    INNER JOIN MyMoney.Transactions t
        ON  t.CategoryId        = c.CategoryId
        AND t.UserId            = @UserId
        AND t.IsActive          = 1
        AND t.TransactionTypeId = 2
        AND t.TransactionDate   >= @MonthStart
        AND t.TransactionDate   <  @NextMonthStart
    WHERE c.IsActive = 1
    GROUP BY c.CategoryId, c.NameEn, c.NameAr
    HAVING ISNULL(SUM(t.Amount), 0) > 0
    ORDER BY TotalAmount DESC;

    -- ── Result Set 4: 10 most recent transactions ─────────────────────────────
    SELECT TOP 10
        t.TransactionId,
        t.Amount,
        t.TransactionTypeId,
        t.Description,
        CONVERT(NVARCHAR(10), t.TransactionDate, 23) AS TransactionDate,
        c.NameEn       AS CategoryNameEn,
        c.NameAr       AS CategoryNameAr,
        c.IconFileName AS CategoryIcon
    FROM MyMoney.Transactions t
    INNER JOIN MyMoney.Categories c ON c.CategoryId = t.CategoryId
    WHERE t.UserId   = @UserId
      AND t.IsActive = 1
    ORDER BY t.TransactionDate DESC, t.TransactionId DESC;

END;
GO
