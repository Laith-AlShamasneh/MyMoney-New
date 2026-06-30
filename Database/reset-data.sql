/* ============================================================================
   MyMoney — Reset transactional data (keep lookups / seed tables)

   Clears all main/transactional/account data so the system starts clean, while
   leaving the lookup, type, and seed tables (Categories, Currencies, Roles,
   WorkspacePermissions, NotificationTemplates, …) fully populated.

   Safe to re-run. Everything is wrapped in a transaction: on ANY error it rolls
   back with ZERO changes. To control what survives, edit the @keep list below.

   USAGE
     - SSMS:    open this file and Execute.
     - sqlcmd:  sqlcmd -S "localhost\SQLEXPRESS" -d MyMoney -C -i Database/reset-data.sql

   ALWAYS back up first — see the BACKUP statement at the bottom of this file.
   This deletes data irreversibly (accounts included, with the default @keep list).
   ============================================================================ */

SET QUOTED_IDENTIFIER ON;   -- REQUIRED: some tables have filtered indexes; DELETE
SET ANSI_NULLS ON;          -- fails without this.
SET NOCOUNT ON;
SET XACT_ABORT ON;

USE [MyMoney];

BEGIN TRY
    BEGIN TRAN;

    /* ----------------------------------------------------------------------
       Tables to KEEP (lookups / types / seed / migration history).
       Everything NOT listed here is emptied.

       To keep your login + workspaces next time (clear financial data only),
       add these to the list as well:
         ('Users'),('Persons'),('UserRoles'),
         ('Workspaces'),('WorkspaceMembers'),
         ('UserCurrencyPreferences'),('UserNotificationPreferences'),
         ('UserWorkspacePreferences'),('UserOnboarding'),('UserOnboardingSteps')
       ---------------------------------------------------------------------- */
    DECLARE @keep TABLE (name sysname PRIMARY KEY);
    INSERT INTO @keep VALUES
     ('Categories'),('Currencies'),('ExchangeRateProviders'),('ExchangeRates'),
     ('NotificationTemplates'),('NotificationTemplateTranslations'),('OnboardingSteps'),
     ('ReportTypes'),('Roles'),('TransactionTypes'),('WorkspacePermissions'),
     ('WorkspaceRolePermissions'),('WorkspaceRoles'),('SchemaMigrations');

    DECLARE @sql NVARCHAR(MAX);

    -- 1. Disable all FK constraints (so delete order doesn't matter)
    SET @sql = N'';
    SELECT @sql += 'ALTER TABLE ' + QUOTENAME(s.name)+'.'+QUOTENAME(t.name) + ' NOCHECK CONSTRAINT ALL;'+CHAR(10)
    FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id;
    EXEC sp_executesql @sql;

    -- 2. Delete from every table NOT in @keep
    SET @sql = N'';
    SELECT @sql += 'DELETE FROM ' + QUOTENAME(s.name)+'.'+QUOTENAME(t.name) + ';'+CHAR(10)
    FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE t.name NOT IN (SELECT name FROM @keep);
    EXEC sp_executesql @sql;

    -- 3. Reseed identity columns on cleared tables (next insert starts at 1)
    SET @sql = N'';
    SELECT @sql += 'DBCC CHECKIDENT('''+s.name+'.'+t.name+''', RESEED, 0) WITH NO_INFOMSGS;'+CHAR(10)
    FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE t.name NOT IN (SELECT name FROM @keep)
      AND EXISTS (SELECT 1 FROM sys.identity_columns ic WHERE ic.object_id = t.object_id);
    EXEC sp_executesql @sql;

    -- 4. Re-enable + revalidate all FK constraints
    SET @sql = N'';
    SELECT @sql += 'ALTER TABLE ' + QUOTENAME(s.name)+'.'+QUOTENAME(t.name) + ' WITH CHECK CHECK CONSTRAINT ALL;'+CHAR(10)
    FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id;
    EXEC sp_executesql @sql;

    COMMIT;
    PRINT 'WIPE COMPLETE.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    PRINT 'ERROR (rolled back): ' + ERROR_MESSAGE();
    THROW;
END CATCH
GO

/* ----------------------------------------------------------------------------
   BACK UP FIRST — run this BEFORE the script above (writes to the SQL Server
   instance's default backup folder). Restore with RESTORE DATABASE if needed.

   BACKUP DATABASE [MyMoney]
     TO DISK = N'MyMoney_prewipe.bak'
     WITH INIT, FORMAT, NAME = N'MyMoney pre-wipe';
   ---------------------------------------------------------------------------- */
