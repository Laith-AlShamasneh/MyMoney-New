-- =============================================================================
-- MyMoney.usp_BackgroundJob_PickUp
-- Atomically selects the next batch of due jobs (Pending or Failed-with-retry)
-- and marks them as Processing to prevent concurrent pickup.
-- StatusId: 1=Pending  2=Processing  4=Failed
-- Priority: 1=High  2=Normal  3=Low  (lower number = higher priority)
-- =============================================================================

CREATE OR ALTER PROCEDURE MyMoney.usp_BackgroundJob_PickUp
    @BatchSize INT = 20
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PickedUpAt DATETIME2(0) = GETUTCDATE();

    -- Atomically claim eligible jobs
    UPDATE MyMoney.BackgroundJobs
    SET
        StatusId    = 2,                    -- Processing
        PickedUpAt  = @PickedUpAt,
        LastAttemptAt = @PickedUpAt,
        AttemptCount  = AttemptCount + 1
    OUTPUT
        INSERTED.JobId,
        INSERTED.JobType,
        INSERTED.Payload,
        INSERTED.AttemptCount,
        INSERTED.MaxAttempts
    WHERE JobId IN
    (
        SELECT TOP (@BatchSize) JobId
        FROM MyMoney.BackgroundJobs WITH (UPDLOCK, READPAST)
        WHERE
        (
            -- Pending jobs scheduled for now or the past
            (StatusId = 1 AND ScheduledAt <= @PickedUpAt)
            OR
            -- Failed jobs within retry budget and due for retry
            (StatusId = 4
             AND AttemptCount < MaxAttempts
             AND NextRetryAt IS NOT NULL
             AND NextRetryAt <= @PickedUpAt)
        )
        ORDER BY Priority ASC, ScheduledAt ASC
    );
END
GO
