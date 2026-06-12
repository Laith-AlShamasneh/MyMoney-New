-- =============================================================================
-- MyMoney.usp_BackgroundJob_Fail
-- Marks a job as failed. If retries remain, sets NextRetryAt using
-- exponential backoff: 2^AttemptCount minutes (1, 2, 4, 8, ...).
-- StatusId 4 = Failed
-- =============================================================================

CREATE OR ALTER PROCEDURE MyMoney.usp_BackgroundJob_Fail
    @JobId        BIGINT,
    @ErrorMessage NVARCHAR(MAX),
    @AttemptCount INT,
    @MaxAttempts  INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @NextRetryAt DATETIME2(0) = NULL;

    -- Set exponential backoff only when retries remain
    IF @AttemptCount < @MaxAttempts
    BEGIN
        -- Backoff in minutes: 2^AttemptCount (attempt 1 = 2 min, 2 = 4 min, 3 = 8 min)
        SET @NextRetryAt = DATEADD(MINUTE, POWER(2, @AttemptCount), GETUTCDATE());
    END

    UPDATE MyMoney.BackgroundJobs
    SET
        StatusId     = 4,
        ErrorMessage = @ErrorMessage,
        NextRetryAt  = @NextRetryAt
    WHERE JobId = @JobId;
END
GO
