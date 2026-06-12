-- =============================================================================
-- MyMoney.usp_BackgroundJob_Enqueue
-- Inserts a new job into the background jobs queue with Pending status.
-- StatusId 1 = Pending
-- =============================================================================

CREATE OR ALTER PROCEDURE MyMoney.usp_BackgroundJob_Enqueue
    @JobType     NVARCHAR(200),
    @Payload     NVARCHAR(MAX),
    @Priority    TINYINT       = 2,
    @ScheduledAt DATETIME2(0)  = NULL,
    @MaxAttempts INT           = 3,
    @CreatedBy   BIGINT        = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO MyMoney.BackgroundJobs
        (JobType, Payload, StatusId, Priority, ScheduledAt, MaxAttempts, CreatedAt, CreatedBy)
    VALUES
        (@JobType, @Payload, 1, @Priority,
         ISNULL(@ScheduledAt, GETUTCDATE()),
         @MaxAttempts, GETUTCDATE(), @CreatedBy);
END
GO
