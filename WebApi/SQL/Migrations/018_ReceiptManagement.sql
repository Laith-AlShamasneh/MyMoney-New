-- ============================================================
-- Receipt Management System
-- Migration 018
-- ============================================================
USE [MyMoney];
GO

-- ============================================================
-- 1. TABLES
-- ============================================================

CREATE TABLE [MyMoney].[Receipts] (
    [ReceiptId]           BIGINT          IDENTITY(1,1) NOT NULL,
    [UserId]              BIGINT          NOT NULL,
    [TransactionId]       BIGINT          NULL,
    [OriginalFileName]    NVARCHAR(255)   NOT NULL,
    [StoredFileName]      NVARCHAR(500)   NOT NULL,
    [FileExtension]       NVARCHAR(20)    NOT NULL,
    [FileSizeBytes]       BIGINT          NOT NULL,
    [ContentType]         NVARCHAR(100)   NOT NULL,
    [FileHash]            NVARCHAR(64)    NOT NULL,
    [Title]               NVARCHAR(255)   NULL,
    [Description]         NVARCHAR(1000)  NULL,
    [ReceiptDate]         DATE            NULL,
    [MerchantName]        NVARCHAR(255)   NULL,
    [Amount]              DECIMAL(18, 2)  NULL,
    [CurrencyCode]        NVARCHAR(10)    NULL,
    [Notes]               NVARCHAR(2000)  NULL,
    [StatusId]            TINYINT         NOT NULL CONSTRAINT [DF_Receipts_StatusId] DEFAULT (1),
    [ProcessingStatusId]  TINYINT         NOT NULL CONSTRAINT [DF_Receipts_ProcessingStatusId] DEFAULT (1),
    [OcrProcessed]        BIT             NOT NULL CONSTRAINT [DF_Receipts_OcrProcessed] DEFAULT (0),
    [ThumbnailFileName]   NVARCHAR(500)   NULL,
    [CreatedOnUtc]        DATETIME2(7)    NOT NULL CONSTRAINT [DF_Receipts_CreatedOnUtc] DEFAULT (GETUTCDATE()),
    [UpdatedOnUtc]        DATETIME2(7)    NULL,
    [ArchivedOnUtc]       DATETIME2(7)    NULL,
    [DeletedOnUtc]        DATETIME2(7)    NULL,
    CONSTRAINT [PK_Receipts] PRIMARY KEY CLUSTERED ([ReceiptId] ASC),
    CONSTRAINT [FK_Receipts_Users]        FOREIGN KEY ([UserId])        REFERENCES [MyMoney].[Users]([UserId]),
    CONSTRAINT [FK_Receipts_Transactions] FOREIGN KEY ([TransactionId]) REFERENCES [MyMoney].[Transactions]([TransactionId])
);
GO

CREATE INDEX [IX_Receipts_UserId_StatusId]
    ON [MyMoney].[Receipts] ([UserId], [StatusId]);
GO

CREATE INDEX [IX_Receipts_UserId_ReceiptDate]
    ON [MyMoney].[Receipts] ([UserId], [ReceiptDate]);
GO

CREATE INDEX [IX_Receipts_TransactionId]
    ON [MyMoney].[Receipts] ([TransactionId])
    WHERE [TransactionId] IS NOT NULL;
GO

CREATE INDEX [IX_Receipts_FileHash]
    ON [MyMoney].[Receipts] ([FileHash]);
GO

CREATE INDEX [IX_Receipts_ProcessingStatusId]
    ON [MyMoney].[Receipts] ([ProcessingStatusId])
    WHERE [ProcessingStatusId] = 1;  -- Pending only
GO

-- ── ReceiptTags ──────────────────────────────────────────────────────────────

CREATE TABLE [MyMoney].[ReceiptTags] (
    [TagId]        INT           IDENTITY(1,1) NOT NULL,
    [UserId]       BIGINT        NOT NULL,
    [Name]         NVARCHAR(100) NOT NULL,
    [ColorHex]     NVARCHAR(10)  NULL,
    [CreatedOnUtc] DATETIME2(7)  NOT NULL CONSTRAINT [DF_ReceiptTags_CreatedOnUtc] DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_ReceiptTags]        PRIMARY KEY CLUSTERED ([TagId] ASC),
    CONSTRAINT [FK_ReceiptTags_Users]  FOREIGN KEY ([UserId]) REFERENCES [MyMoney].[Users]([UserId]),
    CONSTRAINT [UQ_ReceiptTags_UserName] UNIQUE ([UserId], [Name])
);
GO

CREATE INDEX [IX_ReceiptTags_UserId]
    ON [MyMoney].[ReceiptTags] ([UserId]);
GO

-- ── ReceiptTagMappings ────────────────────────────────────────────────────────

CREATE TABLE [MyMoney].[ReceiptTagMappings] (
    [ReceiptId]     BIGINT       NOT NULL,
    [TagId]         INT          NOT NULL,
    [AssignedOnUtc] DATETIME2(7) NOT NULL CONSTRAINT [DF_ReceiptTagMappings_AssignedOnUtc] DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_ReceiptTagMappings]          PRIMARY KEY CLUSTERED ([ReceiptId], [TagId]),
    CONSTRAINT [FK_ReceiptTagMappings_Receipts] FOREIGN KEY ([ReceiptId]) REFERENCES [MyMoney].[Receipts]([ReceiptId]),
    CONSTRAINT [FK_ReceiptTagMappings_Tags]     FOREIGN KEY ([TagId])     REFERENCES [MyMoney].[ReceiptTags]([TagId])
);
GO

CREATE INDEX [IX_ReceiptTagMappings_TagId]
    ON [MyMoney].[ReceiptTagMappings] ([TagId]);
GO

-- ── ReceiptOcrResults ─────────────────────────────────────────────────────────

CREATE TABLE [MyMoney].[ReceiptOcrResults] (
    [OcrResultId]    BIGINT         IDENTITY(1,1) NOT NULL,
    [ReceiptId]      BIGINT         NOT NULL,
    [RawText]        NVARCHAR(MAX)  NULL,
    [MerchantName]   NVARCHAR(255)  NULL,
    [TotalAmount]    DECIMAL(18, 2) NULL,
    [ReceiptDate]    DATE           NULL,
    [Confidence]     DECIMAL(5, 4)  NULL,
    [ProviderName]   NVARCHAR(100)  NULL,
    [ProcessedOnUtc] DATETIME2(7)   NOT NULL CONSTRAINT [DF_ReceiptOcrResults_ProcessedOnUtc] DEFAULT (GETUTCDATE()),
    [ErrorMessage]   NVARCHAR(MAX)  NULL,
    CONSTRAINT [PK_ReceiptOcrResults]          PRIMARY KEY CLUSTERED ([OcrResultId] ASC),
    CONSTRAINT [FK_ReceiptOcrResults_Receipts] FOREIGN KEY ([ReceiptId]) REFERENCES [MyMoney].[Receipts]([ReceiptId])
);
GO

CREATE INDEX [IX_ReceiptOcrResults_ReceiptId]
    ON [MyMoney].[ReceiptOcrResults] ([ReceiptId]);
GO


-- ============================================================
-- 2. STORED PROCEDURES
-- ============================================================

-- ── usp_Receipt_Upload ────────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Receipt_Upload]
    @UserId           BIGINT,
    @OriginalFileName NVARCHAR(255),
    @StoredFileName   NVARCHAR(500),
    @FileExtension    NVARCHAR(20),
    @FileSizeBytes    BIGINT,
    @ContentType      NVARCHAR(100),
    @FileHash         NVARCHAR(64),
    @Title            NVARCHAR(255)   = NULL,
    @Description      NVARCHAR(1000)  = NULL,
    @ReceiptDate      DATE            = NULL,
    @MerchantName     NVARCHAR(255)   = NULL,
    @Amount           DECIMAL(18, 2)  = NULL,
    @CurrencyCode     NVARCHAR(10)    = NULL,
    @Notes            NVARCHAR(2000)  = NULL,
    @ReceiptId        BIGINT          OUTPUT,
    @IsDuplicate      BIT             OUTPUT,
    @DuplicateId      BIGINT          OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @ReceiptId   = 0;
    SET @IsDuplicate = 0;
    SET @DuplicateId = 0;

    -- Check for duplicate (same user, same hash, not deleted)
    SELECT TOP 1 @DuplicateId = [ReceiptId]
    FROM [MyMoney].[Receipts]
    WHERE [UserId]   = @UserId
      AND [FileHash] = @FileHash
      AND [StatusId] <> 3;  -- not deleted

    IF @DuplicateId > 0
    BEGIN
        SET @IsDuplicate = 1;
        RETURN;
    END;

    INSERT INTO [MyMoney].[Receipts] (
        [UserId], [OriginalFileName], [StoredFileName], [FileExtension],
        [FileSizeBytes], [ContentType], [FileHash], [Title], [Description],
        [ReceiptDate], [MerchantName], [Amount], [CurrencyCode], [Notes],
        [StatusId], [ProcessingStatusId], [OcrProcessed], [CreatedOnUtc])
    VALUES (
        @UserId, @OriginalFileName, @StoredFileName, @FileExtension,
        @FileSizeBytes, @ContentType, @FileHash, @Title, @Description,
        @ReceiptDate, @MerchantName, @Amount, @CurrencyCode, @Notes,
        1, 1, 0, GETUTCDATE());

    SET @ReceiptId = SCOPE_IDENTITY();
END;
GO

-- ── usp_Receipt_GetById ───────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Receipt_GetById]
    @UserId    BIGINT,
    @ReceiptId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    -- Result set 1: receipt details
    SELECT
        r.[ReceiptId],
        r.[UserId],
        r.[TransactionId],
        r.[OriginalFileName],
        r.[StoredFileName],
        r.[FileExtension],
        r.[FileSizeBytes],
        r.[ContentType],
        r.[Title],
        r.[Description],
        r.[ReceiptDate],
        r.[MerchantName],
        r.[Amount],
        r.[CurrencyCode],
        r.[Notes],
        r.[StatusId],
        r.[ProcessingStatusId],
        r.[OcrProcessed],
        r.[ThumbnailFileName],
        r.[CreatedOnUtc],
        r.[UpdatedOnUtc]
    FROM [MyMoney].[Receipts] r
    WHERE r.[ReceiptId] = @ReceiptId
      AND r.[UserId]    = @UserId
      AND r.[StatusId] <> 3;  -- not deleted

    -- Result set 2: tags
    SELECT
        t.[TagId],
        t.[Name],
        t.[ColorHex]
    FROM [MyMoney].[ReceiptTagMappings] m
    INNER JOIN [MyMoney].[ReceiptTags]  t ON t.[TagId] = m.[TagId]
    WHERE m.[ReceiptId] = @ReceiptId;
END;
GO

-- ── usp_Receipt_Search ────────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Receipt_Search]
    @UserId       BIGINT,
    @Keyword      NVARCHAR(500)  = NULL,
    @StatusId     TINYINT        = NULL,
    @DateFrom     DATE           = NULL,
    @DateTo       DATE           = NULL,
    @AmountMin    DECIMAL(18, 2) = NULL,
    @AmountMax    DECIMAL(18, 2) = NULL,
    @TagId        INT            = NULL,
    @PageNumber   INT            = 1,
    @PageSize     INT            = 20,
    @TotalCount   INT            OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    -- Count (exclude deleted receipts by default unless explicitly requested)
    SELECT @TotalCount = COUNT(*)
    FROM [MyMoney].[Receipts] r
    WHERE r.[UserId]   = @UserId
      AND r.[StatusId] <> 3
      AND (@StatusId   IS NULL OR r.[StatusId]    = @StatusId)
      AND (@DateFrom   IS NULL OR r.[ReceiptDate] >= @DateFrom)
      AND (@DateTo     IS NULL OR r.[ReceiptDate] <= @DateTo)
      AND (@AmountMin  IS NULL OR r.[Amount]      >= @AmountMin)
      AND (@AmountMax  IS NULL OR r.[Amount]      <= @AmountMax)
      AND (@Keyword    IS NULL
           OR r.[Title]        LIKE N'%' + @Keyword + N'%'
           OR r.[MerchantName] LIKE N'%' + @Keyword + N'%'
           OR r.[Description]  LIKE N'%' + @Keyword + N'%'
           OR r.[Notes]        LIKE N'%' + @Keyword + N'%'
           OR r.[OriginalFileName] LIKE N'%' + @Keyword + N'%')
      AND (@TagId      IS NULL
           OR EXISTS (
               SELECT 1 FROM [MyMoney].[ReceiptTagMappings] m
               WHERE m.[ReceiptId] = r.[ReceiptId] AND m.[TagId] = @TagId));

    -- Results
    SELECT
        r.[ReceiptId],
        r.[OriginalFileName],
        r.[StoredFileName],
        r.[FileExtension],
        r.[FileSizeBytes],
        r.[ContentType],
        r.[Title],
        r.[ReceiptDate],
        r.[MerchantName],
        r.[Amount],
        r.[CurrencyCode],
        r.[StatusId],
        r.[ProcessingStatusId],
        r.[OcrProcessed],
        r.[ThumbnailFileName],
        r.[TransactionId],
        r.[CreatedOnUtc],
        r.[UpdatedOnUtc],
        (SELECT COUNT(*) FROM [MyMoney].[ReceiptTagMappings] m WHERE m.[ReceiptId] = r.[ReceiptId]) AS [TagCount]
    FROM [MyMoney].[Receipts] r
    WHERE r.[UserId]   = @UserId
      AND r.[StatusId] <> 3
      AND (@StatusId   IS NULL OR r.[StatusId]    = @StatusId)
      AND (@DateFrom   IS NULL OR r.[ReceiptDate] >= @DateFrom)
      AND (@DateTo     IS NULL OR r.[ReceiptDate] <= @DateTo)
      AND (@AmountMin  IS NULL OR r.[Amount]      >= @AmountMin)
      AND (@AmountMax  IS NULL OR r.[Amount]      <= @AmountMax)
      AND (@Keyword    IS NULL
           OR r.[Title]        LIKE N'%' + @Keyword + N'%'
           OR r.[MerchantName] LIKE N'%' + @Keyword + N'%'
           OR r.[Description]  LIKE N'%' + @Keyword + N'%'
           OR r.[Notes]        LIKE N'%' + @Keyword + N'%'
           OR r.[OriginalFileName] LIKE N'%' + @Keyword + N'%')
      AND (@TagId      IS NULL
           OR EXISTS (
               SELECT 1 FROM [MyMoney].[ReceiptTagMappings] m
               WHERE m.[ReceiptId] = r.[ReceiptId] AND m.[TagId] = @TagId))
    ORDER BY r.[CreatedOnUtc] DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END;
GO

-- ── usp_Receipt_Update ────────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Receipt_Update]
    @UserId       BIGINT,
    @ReceiptId    BIGINT,
    @Title        NVARCHAR(255)  = NULL,
    @Description  NVARCHAR(1000) = NULL,
    @ReceiptDate  DATE           = NULL,
    @MerchantName NVARCHAR(255)  = NULL,
    @Amount       DECIMAL(18, 2) = NULL,
    @CurrencyCode NVARCHAR(10)   = NULL,
    @Notes        NVARCHAR(2000) = NULL,
    @RowsAffected INT            OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [MyMoney].[Receipts]
    SET
        [Title]        = @Title,
        [Description]  = @Description,
        [ReceiptDate]  = @ReceiptDate,
        [MerchantName] = @MerchantName,
        [Amount]       = @Amount,
        [CurrencyCode] = @CurrencyCode,
        [Notes]        = @Notes,
        [UpdatedOnUtc] = GETUTCDATE()
    WHERE [ReceiptId] = @ReceiptId
      AND [UserId]    = @UserId
      AND [StatusId] <> 3;

    SET @RowsAffected = @@ROWCOUNT;
END;
GO

-- ── usp_Receipt_Delete ────────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Receipt_Delete]
    @UserId        BIGINT,
    @ReceiptId     BIGINT,
    @StoredFileName  NVARCHAR(500) OUTPUT,
    @ThumbnailFileName NVARCHAR(500) OUTPUT,
    @RowsAffected  INT             OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @StoredFileName    = NULL;
    SET @ThumbnailFileName = NULL;

    SELECT
        @StoredFileName    = [StoredFileName],
        @ThumbnailFileName = [ThumbnailFileName]
    FROM [MyMoney].[Receipts]
    WHERE [ReceiptId] = @ReceiptId
      AND [UserId]    = @UserId
      AND [StatusId] <> 3;

    IF @StoredFileName IS NULL
    BEGIN
        SET @RowsAffected = 0;
        RETURN;
    END;

    UPDATE [MyMoney].[Receipts]
    SET
        [StatusId]     = 3,
        [DeletedOnUtc] = GETUTCDATE()
    WHERE [ReceiptId] = @ReceiptId
      AND [UserId]    = @UserId;

    SET @RowsAffected = @@ROWCOUNT;
END;
GO

-- ── usp_Receipt_Archive ───────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Receipt_Archive]
    @UserId       BIGINT,
    @ReceiptId    BIGINT,
    @RowsAffected INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [MyMoney].[Receipts]
    SET
        [StatusId]      = 2,
        [ArchivedOnUtc] = GETUTCDATE()
    WHERE [ReceiptId] = @ReceiptId
      AND [UserId]    = @UserId
      AND [StatusId]  = 1;  -- only Active → Archived

    SET @RowsAffected = @@ROWCOUNT;
END;
GO

-- ── usp_Receipt_Restore ───────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Receipt_Restore]
    @UserId       BIGINT,
    @ReceiptId    BIGINT,
    @RowsAffected INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [MyMoney].[Receipts]
    SET
        [StatusId]      = 1,
        [ArchivedOnUtc] = NULL,
        [UpdatedOnUtc]  = GETUTCDATE()
    WHERE [ReceiptId] = @ReceiptId
      AND [UserId]    = @UserId
      AND [StatusId]  = 2;  -- only Archived → Active

    SET @RowsAffected = @@ROWCOUNT;
END;
GO

-- ── usp_Receipt_AssignTransaction ─────────────────────────────────────────────

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Receipt_AssignTransaction]
    @UserId        BIGINT,
    @ReceiptId     BIGINT,
    @TransactionId BIGINT = NULL,
    @RowsAffected  INT    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Verify the transaction belongs to the same user (if linking)
    IF @TransactionId IS NOT NULL
    BEGIN
        IF NOT EXISTS (
            SELECT 1 FROM [MyMoney].[Transactions]
            WHERE [TransactionId] = @TransactionId AND [UserId] = @UserId)
        BEGIN
            SET @RowsAffected = -1;  -- transaction not found / not owned
            RETURN;
        END;
    END;

    UPDATE [MyMoney].[Receipts]
    SET
        [TransactionId] = @TransactionId,
        [UpdatedOnUtc]  = GETUTCDATE()
    WHERE [ReceiptId] = @ReceiptId
      AND [UserId]    = @UserId
      AND [StatusId] <> 3;

    SET @RowsAffected = @@ROWCOUNT;
END;
GO

-- ── usp_Receipt_GetDashboard ──────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Receipt_GetDashboard]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    -- Result set 1: summary stats
    SELECT
        COUNT(*)                                              AS [TotalCount],
        SUM(CASE WHEN [StatusId] = 1 THEN 1 ELSE 0 END)     AS [ActiveCount],
        SUM(CASE WHEN [StatusId] = 2 THEN 1 ELSE 0 END)     AS [ArchivedCount],
        SUM(CASE WHEN [OcrProcessed] = 1 THEN 1 ELSE 0 END) AS [OcrProcessedCount],
        SUM(CASE WHEN [ProcessingStatusId] = 4 THEN 1 ELSE 0 END) AS [OcrFailedCount],
        ISNULL(SUM([FileSizeBytes]), 0)                      AS [TotalSizeBytes],
        SUM(CASE WHEN [TransactionId] IS NOT NULL THEN 1 ELSE 0 END) AS [LinkedToTransactionCount]
    FROM [MyMoney].[Receipts]
    WHERE [UserId]   = @UserId
      AND [StatusId] <> 3;

    -- Result set 2: 10 most recent receipts
    SELECT TOP 10
        r.[ReceiptId],
        r.[OriginalFileName],
        r.[StoredFileName],
        r.[FileExtension],
        r.[Title],
        r.[ReceiptDate],
        r.[MerchantName],
        r.[Amount],
        r.[CurrencyCode],
        r.[StatusId],
        r.[OcrProcessed],
        r.[ThumbnailFileName],
        r.[TransactionId],
        r.[CreatedOnUtc]
    FROM [MyMoney].[Receipts] r
    WHERE r.[UserId]   = @UserId
      AND r.[StatusId] <> 3
    ORDER BY r.[CreatedOnUtc] DESC;
END;
GO

-- ── usp_Receipt_Tag_GetList ───────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Receipt_Tag_GetList]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        t.[TagId],
        t.[Name],
        t.[ColorHex],
        t.[CreatedOnUtc],
        (SELECT COUNT(*) FROM [MyMoney].[ReceiptTagMappings] m
         INNER JOIN [MyMoney].[Receipts] r ON r.[ReceiptId] = m.[ReceiptId]
         WHERE m.[TagId] = t.[TagId] AND r.[StatusId] <> 3) AS [UsageCount]
    FROM [MyMoney].[ReceiptTags] t
    WHERE t.[UserId] = @UserId
    ORDER BY t.[Name] ASC;
END;
GO

-- ── usp_Receipt_Tag_Create ────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Receipt_Tag_Create]
    @UserId       BIGINT,
    @Name         NVARCHAR(100),
    @ColorHex     NVARCHAR(10) = NULL,
    @TagId        INT          OUTPUT,
    @IsDuplicate  BIT          OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @TagId       = 0;
    SET @IsDuplicate = 0;

    IF EXISTS (SELECT 1 FROM [MyMoney].[ReceiptTags] WHERE [UserId] = @UserId AND [Name] = @Name)
    BEGIN
        SET @IsDuplicate = 1;
        RETURN;
    END;

    INSERT INTO [MyMoney].[ReceiptTags] ([UserId], [Name], [ColorHex], [CreatedOnUtc])
    VALUES (@UserId, @Name, @ColorHex, GETUTCDATE());

    SET @TagId = SCOPE_IDENTITY();
END;
GO

-- ── usp_Receipt_Tag_Delete ────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Receipt_Tag_Delete]
    @UserId       BIGINT,
    @TagId        INT,
    @RowsAffected INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Cascade: remove all mappings first
    DELETE FROM [MyMoney].[ReceiptTagMappings]
    WHERE [TagId] = @TagId
      AND EXISTS (
          SELECT 1 FROM [MyMoney].[ReceiptTags] t
          WHERE t.[TagId] = @TagId AND t.[UserId] = @UserId);

    -- Delete the tag
    DELETE FROM [MyMoney].[ReceiptTags]
    WHERE [TagId]  = @TagId
      AND [UserId] = @UserId;

    SET @RowsAffected = @@ROWCOUNT;
END;
GO

-- ── usp_Receipt_Tags_Set ──────────────────────────────────────────────────────
-- Atomically replaces all tags on a receipt with a new set.
-- @TagIds is a JSON array of INT, e.g. [1,2,3] or [] to clear all.

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Receipt_Tags_Set]
    @UserId    BIGINT,
    @ReceiptId BIGINT,
    @TagIds    NVARCHAR(MAX)  -- JSON array
AS
BEGIN
    SET NOCOUNT ON;

    -- Verify ownership
    IF NOT EXISTS (
        SELECT 1 FROM [MyMoney].[Receipts]
        WHERE [ReceiptId] = @ReceiptId AND [UserId] = @UserId AND [StatusId] <> 3)
    BEGIN
        RETURN;
    END;

    -- Remove existing tags
    DELETE FROM [MyMoney].[ReceiptTagMappings] WHERE [ReceiptId] = @ReceiptId;

    -- Insert new tags (only those owned by the user)
    INSERT INTO [MyMoney].[ReceiptTagMappings] ([ReceiptId], [TagId], [AssignedOnUtc])
    SELECT @ReceiptId, t.[TagId], GETUTCDATE()
    FROM OPENJSON(@TagIds) WITH ([Value] INT '$') ids
    INNER JOIN [MyMoney].[ReceiptTags] t ON t.[TagId] = ids.[Value] AND t.[UserId] = @UserId;
END;
GO

-- ── usp_Receipt_OcrResult_Save ────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Receipt_OcrResult_Save]
    @ReceiptId     BIGINT,
    @RawText       NVARCHAR(MAX)  = NULL,
    @MerchantName  NVARCHAR(255)  = NULL,
    @TotalAmount   DECIMAL(18, 2) = NULL,
    @ReceiptDate   DATE           = NULL,
    @Confidence    DECIMAL(5, 4)  = NULL,
    @ProviderName  NVARCHAR(100)  = NULL,
    @ErrorMessage  NVARCHAR(MAX)  = NULL,
    @IsSuccess     BIT,
    @OcrResultId   BIGINT         OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ProcessingStatus TINYINT = CASE WHEN @IsSuccess = 1 THEN 3 ELSE 4 END;

    INSERT INTO [MyMoney].[ReceiptOcrResults] (
        [ReceiptId], [RawText], [MerchantName], [TotalAmount], [ReceiptDate],
        [Confidence], [ProviderName], [ProcessedOnUtc], [ErrorMessage])
    VALUES (
        @ReceiptId, @RawText, @MerchantName, @TotalAmount, @ReceiptDate,
        @Confidence, @ProviderName, GETUTCDATE(), @ErrorMessage);

    SET @OcrResultId = SCOPE_IDENTITY();

    -- Update the receipt: mark as processed, auto-fill empty fields from OCR
    UPDATE [MyMoney].[Receipts]
    SET
        [OcrProcessed]       = 1,
        [ProcessingStatusId] = @ProcessingStatus,
        [MerchantName]  = CASE WHEN [MerchantName] IS NULL AND @MerchantName IS NOT NULL THEN @MerchantName ELSE [MerchantName] END,
        [Amount]        = CASE WHEN [Amount]        IS NULL AND @TotalAmount  IS NOT NULL THEN @TotalAmount  ELSE [Amount] END,
        [ReceiptDate]   = CASE WHEN [ReceiptDate]   IS NULL AND @ReceiptDate  IS NOT NULL THEN @ReceiptDate  ELSE [ReceiptDate] END,
        [UpdatedOnUtc]  = GETUTCDATE()
    WHERE [ReceiptId] = @ReceiptId;
END;
GO

-- ── usp_Receipt_SetProcessingStatus ──────────────────────────────────────────

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Receipt_SetProcessingStatus]
    @ReceiptId          BIGINT,
    @ProcessingStatusId TINYINT,
    @RowsAffected       INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [MyMoney].[Receipts]
    SET [ProcessingStatusId] = @ProcessingStatusId
    WHERE [ReceiptId] = @ReceiptId;

    SET @RowsAffected = @@ROWCOUNT;
END;
GO

-- ── usp_Receipt_GetPendingOcr ─────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Receipt_GetPendingOcr]
    @BatchSize INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@BatchSize)
        [ReceiptId],
        [StoredFileName],
        [FileExtension],
        [ContentType]
    FROM [MyMoney].[Receipts]
    WHERE [ProcessingStatusId] = 1   -- Pending
      AND [StatusId]           <> 3  -- not deleted
      AND [OcrProcessed]       = 0
    ORDER BY [CreatedOnUtc] ASC;
END;
GO
