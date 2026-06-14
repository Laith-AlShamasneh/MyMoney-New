-- ============================================================
-- Procedure : MyMoney.usp_Transaction_Search
-- Description: Paginated, filtered, sorted transaction list.
--              Returns aggregate stats via OUTPUT parameters
--              so the caller avoids a second round-trip.
-- ============================================================
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Transaction_Search]
    @UserId         BIGINT,
    @TypeId         TINYINT        = NULL,
    @CategoryId     INT            = NULL,
    @DateFrom       DATE           = NULL,
    @DateTo         DATE           = NULL,
    @AmountMin      DECIMAL(18,2)  = NULL,
    @AmountMax      DECIMAL(18,2)  = NULL,
    @Search         NVARCHAR(500)  = NULL,
    @SortBy         NVARCHAR(50)   = N'TransactionDate',
    @SortDir        NVARCHAR(4)    = N'DESC',
    @PageNumber     INT            = 1,
    @PageSize       INT            = 20,
    @TotalCount     INT            OUTPUT,
    @TotalIncome    DECIMAL(18,2)  OUTPUT,
    @TotalExpenses  DECIMAL(18,2)  OUTPUT,
    @AvgAmount      DECIMAL(18,2)  OUTPUT,
    @MaxAmount      DECIMAL(18,2)  OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Sanitise sort parameters to prevent SQL injection
    SET @SortBy = CASE @SortBy
        WHEN N'Amount'       THEN N'T.Amount'
        WHEN N'Description'  THEN N'T.Description'
        WHEN N'CategoryName' THEN N'C.NameEn'
        WHEN N'CreatedAt'    THEN N'T.CreatedAt'
        ELSE N'T.TransactionDate'
    END;
    SET @SortDir = CASE WHEN UPPER(@SortDir) = N'ASC' THEN N'ASC' ELSE N'DESC' END;

    -- ── Aggregate stats (uses IX_Transactions_UserId_Date fully) ────────────
    SELECT
        @TotalCount    = COUNT(*),
        @TotalIncome   = ISNULL(SUM(CASE WHEN T.TransactionTypeId = 1 THEN T.Amount ELSE 0 END), 0),
        @TotalExpenses = ISNULL(SUM(CASE WHEN T.TransactionTypeId = 2 THEN T.Amount ELSE 0 END), 0),
        @AvgAmount     = ISNULL(AVG(T.Amount), 0),
        @MaxAmount     = ISNULL(MAX(T.Amount), 0)
    FROM MyMoney.Transactions AS T
    WHERE T.UserId   = @UserId
      AND T.IsActive = 1
      AND (@TypeId     IS NULL OR T.TransactionTypeId = @TypeId)
      AND (@CategoryId IS NULL OR T.CategoryId = @CategoryId)
      AND (@DateFrom   IS NULL OR T.TransactionDate  >= @DateFrom)
      AND (@DateTo     IS NULL OR T.TransactionDate  <= @DateTo)
      AND (@AmountMin  IS NULL OR T.Amount >= @AmountMin)
      AND (@AmountMax  IS NULL OR T.Amount <= @AmountMax)
      AND (@Search     IS NULL
           OR T.Description LIKE N'%' + @Search + N'%'
           OR T.Notes       LIKE N'%' + @Search + N'%');

    -- ── Paged result set (dynamic ORDER BY inside sp_executesql) ─────────────
    DECLARE @Sql NVARCHAR(MAX) = N'
        SELECT
            T.TransactionId,
            T.CategoryId,
            C.NameEn        AS CategoryNameEn,
            C.NameAr        AS CategoryNameAr,
            C.IconFileName  AS CategoryIcon,
            T.TransactionTypeId,
            T.Amount,
            T.Description,
            T.TransactionDate,
            T.Notes,
            T.CreatedAt,
            T.UpdatedAt
        FROM MyMoney.Transactions AS T
        INNER JOIN MyMoney.Categories AS C ON C.CategoryId = T.CategoryId
        WHERE T.UserId   = @UserId
          AND T.IsActive = 1
          AND (@TypeId     IS NULL OR T.TransactionTypeId = @TypeId)
          AND (@CategoryId IS NULL OR T.CategoryId = @CategoryId)
          AND (@DateFrom   IS NULL OR T.TransactionDate  >= @DateFrom)
          AND (@DateTo     IS NULL OR T.TransactionDate  <= @DateTo)
          AND (@AmountMin  IS NULL OR T.Amount >= @AmountMin)
          AND (@AmountMax  IS NULL OR T.Amount <= @AmountMax)
          AND (@Search     IS NULL
               OR T.Description LIKE N''%'' + @Search + N''%''
               OR T.Notes       LIKE N''%'' + @Search + N''%'')
        ORDER BY ' + @SortBy + N' ' + @SortDir + N', T.TransactionId DESC
        OFFSET (@PageNumber - 1) * @PageSize ROWS
        FETCH NEXT @PageSize ROWS ONLY;';

    EXEC sp_executesql @Sql,
        N'@UserId BIGINT, @TypeId TINYINT, @CategoryId INT,
          @DateFrom DATE, @DateTo DATE,
          @AmountMin DECIMAL(18,2), @AmountMax DECIMAL(18,2),
          @Search NVARCHAR(500), @PageNumber INT, @PageSize INT',
        @UserId, @TypeId, @CategoryId,
        @DateFrom, @DateTo,
        @AmountMin, @AmountMax,
        @Search, @PageNumber, @PageSize;
END
GO
