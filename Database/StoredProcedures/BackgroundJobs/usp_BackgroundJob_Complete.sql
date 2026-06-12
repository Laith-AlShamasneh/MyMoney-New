-- =============================================================================
-- MyMoney.usp_BackgroundJob_Complete
-- Marks a job as successfully completed.
-- StatusId 3 = Completed
-- =============================================================================

CREATE OR ALTER PROCEDURE MyMoney.usp_BackgroundJob_Complete
    @JobId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE MyMoney.BackgroundJobs
    SET
        StatusId    = 3,
        CompletedAt = GETUTCDATE(),
        ErrorMessage = NULL,
        NextRetryAt  = NULL
    WHERE JobId = @JobId;
END
GO
