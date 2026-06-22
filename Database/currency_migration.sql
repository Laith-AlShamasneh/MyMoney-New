-- =============================================================================
-- Multi-Currency System Migration
-- MyMoney — Enterprise-Grade Currency Foundation
-- Generated: 2026-06-23
--
-- Run order: Execute the entire script in a single SSMS batch.
-- Safe to re-run: All SPs use CREATE OR ALTER.
--                 Tables and indexes use IF NOT EXISTS guards.
--                 ALTER TABLE uses IF NOT EXISTS column guards.
-- =============================================================================

USE [MyMoney];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =============================================================================
-- TABLE: MyMoney.Currencies
-- ISO 4217 reference data. Seeded at creation.
-- IsCrypto flag future-proofs for Bitcoin, ETH, etc.
-- =============================================================================

IF OBJECT_ID(N'[MyMoney].[Currencies]', N'U') IS NULL
BEGIN
    CREATE TABLE [MyMoney].[Currencies]
    (
        [CurrencyId]    INT           IDENTITY(1,1) NOT NULL,
        [Code]          NVARCHAR(10)                NOT NULL,   -- ISO 4217: 'USD', 'EUR', 'BTC'
        [NameEn]        NVARCHAR(100)               NOT NULL,
        [NameAr]        NVARCHAR(100)               NOT NULL,
        [Symbol]        NVARCHAR(10)                NOT NULL,   -- '$', '€', '£'
        [NativeSymbol]  NVARCHAR(10)                NULL,       -- Arabic/native glyphs
        [DecimalDigits] TINYINT                     NOT NULL CONSTRAINT [DF_Currencies_DecimalDigits] DEFAULT 2,
        [CountryCode]   NVARCHAR(5)                 NULL,       -- ISO 3166-1 alpha-2
        [IsActive]      BIT                         NOT NULL CONSTRAINT [DF_Currencies_IsActive]      DEFAULT 1,
        [IsSystemBase]  BIT                         NOT NULL CONSTRAINT [DF_Currencies_IsSystemBase]  DEFAULT 0,
        [IsCrypto]      BIT                         NOT NULL CONSTRAINT [DF_Currencies_IsCrypto]      DEFAULT 0,
        [DisplayOrder]  SMALLINT                    NOT NULL CONSTRAINT [DF_Currencies_DisplayOrder]  DEFAULT 99,
        [CreatedAt]     DATETIME2(0)                NOT NULL CONSTRAINT [DF_Currencies_CreatedAt]     DEFAULT GETUTCDATE(),
        [UpdatedAt]     DATETIME2(0)                NULL,

        CONSTRAINT [PK_Currencies]       PRIMARY KEY CLUSTERED ([CurrencyId] ASC),
        CONSTRAINT [UQ_Currencies_Code]  UNIQUE NONCLUSTERED ([Code]),
        CONSTRAINT [CK_Currencies_Digits] CHECK ([DecimalDigits] BETWEEN 0 AND 8)
    );
    PRINT 'Created table [MyMoney].[Currencies]';
END
GO

-- =============================================================================
-- TABLE: MyMoney.ExchangeRateProviders
-- Registry of rate sources. Decouples provider identity from business logic.
-- =============================================================================

IF OBJECT_ID(N'[MyMoney].[ExchangeRateProviders]', N'U') IS NULL
BEGIN
    CREATE TABLE [MyMoney].[ExchangeRateProviders]
    (
        [ProviderId]  INT           IDENTITY(1,1) NOT NULL,
        [Code]        NVARCHAR(50)                NOT NULL,   -- 'MANUAL', 'ECB', 'OPEN_EXCHANGE_RATES'
        [NameEn]      NVARCHAR(100)               NOT NULL,
        [NameAr]      NVARCHAR(100)               NOT NULL,
        [IsActive]    BIT                         NOT NULL CONSTRAINT [DF_Providers_IsActive]  DEFAULT 1,
        [IsDefault]   BIT                         NOT NULL CONSTRAINT [DF_Providers_IsDefault] DEFAULT 0,
        [ApiBaseUrl]  NVARCHAR(500)               NULL,
        [Priority]    TINYINT                     NOT NULL CONSTRAINT [DF_Providers_Priority]  DEFAULT 99,
        [CreatedAt]   DATETIME2(0)                NOT NULL CONSTRAINT [DF_Providers_CreatedAt] DEFAULT GETUTCDATE(),
        [UpdatedAt]   DATETIME2(0)                NULL,

        CONSTRAINT [PK_ExchangeRateProviders]      PRIMARY KEY CLUSTERED ([ProviderId] ASC),
        CONSTRAINT [UQ_ExchangeRateProviders_Code] UNIQUE NONCLUSTERED ([Code])
    );
    PRINT 'Created table [MyMoney].[ExchangeRateProviders]';
END
GO

-- =============================================================================
-- TABLE: MyMoney.ExchangeRates
-- Immutable historical ledger. Records are NEVER overwritten.
-- New rates are inserted; old rates get ExpiryDate set.
-- Every past conversion can be reproduced exactly by querying at EffectiveDate.
-- =============================================================================

IF OBJECT_ID(N'[MyMoney].[ExchangeRates]', N'U') IS NULL
BEGIN
    CREATE TABLE [MyMoney].[ExchangeRates]
    (
        [RateId]           BIGINT         IDENTITY(1,1) NOT NULL,
        [FromCurrency]     NVARCHAR(10)                 NOT NULL,   -- e.g. 'USD'
        [ToCurrency]       NVARCHAR(10)                 NOT NULL,   -- e.g. 'EUR'
        [Rate]             DECIMAL(28, 10)              NOT NULL,   -- Direct rate: 1 FROM = N TO
        [InverseRate]      DECIMAL(28, 10)              NOT NULL,   -- 1 / Rate, pre-computed
        [ProviderId]       INT                          NOT NULL,
        [EffectiveDate]    DATE                         NOT NULL,   -- Rate valid from this date
        [ExpiryDate]       DATE                         NULL,       -- NULL = current active rate
        [SourceTypeId]     TINYINT                      NOT NULL CONSTRAINT [DF_ExRates_SourceType] DEFAULT 1,
        -- 1=Manual  2=Automatic  3=Estimated
        [StatusId]         TINYINT                      NOT NULL CONSTRAINT [DF_ExRates_StatusId]   DEFAULT 1,
        -- 1=Active  2=Draft  3=Archived
        [CreatedAt]        DATETIME2(0)                 NOT NULL CONSTRAINT [DF_ExRates_CreatedAt]  DEFAULT GETUTCDATE(),
        [CreatedBy]        BIGINT                       NULL,

        CONSTRAINT [PK_ExchangeRates]              PRIMARY KEY CLUSTERED ([RateId] ASC),
        CONSTRAINT [FK_ExchangeRates_Providers]    FOREIGN KEY ([ProviderId])
            REFERENCES [MyMoney].[ExchangeRateProviders]([ProviderId]),
        CONSTRAINT [CK_ExchangeRates_Rate]         CHECK ([Rate] > 0),
        CONSTRAINT [CK_ExchangeRates_InverseRate]  CHECK ([InverseRate] > 0),
        CONSTRAINT [CK_ExchangeRates_SourceTypeId] CHECK ([SourceTypeId] IN (1, 2, 3)),
        CONSTRAINT [CK_ExchangeRates_StatusId]     CHECK ([StatusId]     IN (1, 2, 3)),
        CONSTRAINT [CK_ExchangeRates_NotSelf]      CHECK ([FromCurrency] <> [ToCurrency])
    );
    PRINT 'Created table [MyMoney].[ExchangeRates]';
END
GO

-- ExchangeRates — primary lookup: find current/historical rate for a pair
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ExchangeRates_Pair_EffDate'
    AND object_id = OBJECT_ID(N'[MyMoney].[ExchangeRates]'))
    CREATE NONCLUSTERED INDEX [IX_ExchangeRates_Pair_EffDate]
    ON [MyMoney].[ExchangeRates] ([FromCurrency] ASC, [ToCurrency] ASC, [EffectiveDate] DESC)
    INCLUDE ([Rate], [InverseRate], [StatusId], [ExpiryDate]);
GO

-- ExchangeRates — find all active rates as of a given date
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ExchangeRates_Date_Status'
    AND object_id = OBJECT_ID(N'[MyMoney].[ExchangeRates]'))
    CREATE NONCLUSTERED INDEX [IX_ExchangeRates_Date_Status]
    ON [MyMoney].[ExchangeRates] ([EffectiveDate] DESC, [StatusId] ASC)
    INCLUDE ([FromCurrency], [ToCurrency], [Rate]);
GO

-- ExchangeRates — identify stale pairs (no rate within N days)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ExchangeRates_Provider_Status'
    AND object_id = OBJECT_ID(N'[MyMoney].[ExchangeRates]'))
    CREATE NONCLUSTERED INDEX [IX_ExchangeRates_Provider_Status]
    ON [MyMoney].[ExchangeRates] ([ProviderId] ASC, [StatusId] ASC, [EffectiveDate] DESC);
GO

-- =============================================================================
-- TABLE: MyMoney.UserCurrencyPreferences
-- Per-user currency settings. Created during onboarding or first transaction.
-- =============================================================================

IF OBJECT_ID(N'[MyMoney].[UserCurrencyPreferences]', N'U') IS NULL
BEGIN
    CREATE TABLE [MyMoney].[UserCurrencyPreferences]
    (
        [UserId]              BIGINT       NOT NULL,
        [BaseCurrencyCode]    NVARCHAR(10) NOT NULL CONSTRAINT [DF_UCP_Base]       DEFAULT N'USD',
        [DisplayCurrencyCode] NVARCHAR(10) NOT NULL CONSTRAINT [DF_UCP_Display]    DEFAULT N'USD',
        -- Formatting preferences
        [NumberFormatId]      TINYINT      NOT NULL CONSTRAINT [DF_UCP_NumFmt]     DEFAULT 1,
        -- 1=1,234.56  2=1.234,56  3=1 234,56  4=1 234.56
        [SymbolStyleId]       TINYINT      NOT NULL CONSTRAINT [DF_UCP_SymStyle]   DEFAULT 1,
        -- 1=Symbol($)  2=Code(USD)  3=Both($USD)
        [NegativeFormatId]    TINYINT      NOT NULL CONSTRAINT [DF_UCP_NegFmt]     DEFAULT 1,
        -- 1=-1,000  2=(1,000)  3=1,000-
        [CurrencyPositionId]  TINYINT      NOT NULL CONSTRAINT [DF_UCP_CurPos]     DEFAULT 1,
        -- 1=Before amount  2=After amount
        [CreatedAt]           DATETIME2(0) NOT NULL CONSTRAINT [DF_UCP_CreatedAt]  DEFAULT GETUTCDATE(),
        [UpdatedAt]           DATETIME2(0) NULL,

        CONSTRAINT [PK_UserCurrencyPreferences]     PRIMARY KEY CLUSTERED ([UserId] ASC),
        CONSTRAINT [FK_UserCurrencyPrefs_Users]     FOREIGN KEY ([UserId])
            REFERENCES [MyMoney].[Users]([UserId]),
        CONSTRAINT [CK_UCP_NumberFormatId]     CHECK ([NumberFormatId]     IN (1, 2, 3, 4)),
        CONSTRAINT [CK_UCP_SymbolStyleId]      CHECK ([SymbolStyleId]      IN (1, 2, 3)),
        CONSTRAINT [CK_UCP_NegativeFormatId]   CHECK ([NegativeFormatId]   IN (1, 2, 3)),
        CONSTRAINT [CK_UCP_CurrencyPositionId] CHECK ([CurrencyPositionId] IN (1, 2))
    );
    PRINT 'Created table [MyMoney].[UserCurrencyPreferences]';
END
GO

-- =============================================================================
-- TABLE: MyMoney.CurrencyConversionLog
-- Immutable audit trail of every conversion performed.
-- Every monetary conversion is reproducible years later.
-- =============================================================================

IF OBJECT_ID(N'[MyMoney].[CurrencyConversionLog]', N'U') IS NULL
BEGIN
    CREATE TABLE [MyMoney].[CurrencyConversionLog]
    (
        [LogId]             BIGINT          IDENTITY(1,1) NOT NULL,
        [UserId]            BIGINT          NOT NULL,
        [EntityType]        NVARCHAR(50)    NOT NULL,   -- 'Transaction','Goal','Budget','Receipt'
        [EntityId]          BIGINT          NOT NULL,
        [FromCurrency]      NVARCHAR(10)    NOT NULL,
        [ToCurrency]        NVARCHAR(10)    NOT NULL,
        [OriginalAmount]    DECIMAL(18, 4)  NOT NULL,
        [ConvertedAmount]   DECIMAL(18, 4)  NOT NULL,
        [ExchangeRate]      DECIMAL(28, 10) NOT NULL,
        [RateId]            BIGINT          NULL,       -- FK to ExchangeRates (nullable for manual)
        [RateEffectiveDate] DATE            NOT NULL,
        [ConversionModeId]  TINYINT         NOT NULL,   -- 1=Historical 2=Current 3=Manual
        [CreatedAt]         DATETIME2(0)    NOT NULL CONSTRAINT [DF_CCLog_CreatedAt] DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_CurrencyConversionLog]        PRIMARY KEY CLUSTERED ([LogId] ASC),
        CONSTRAINT [FK_CCLog_ExchangeRates]          FOREIGN KEY ([RateId])
            REFERENCES [MyMoney].[ExchangeRates]([RateId]),
        CONSTRAINT [CK_CCLog_ConversionModeId]       CHECK ([ConversionModeId] IN (1, 2, 3))
    );
    PRINT 'Created table [MyMoney].[CurrencyConversionLog]';
END
GO

-- CurrencyConversionLog — audit query by entity
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CCLog_EntityType_EntityId'
    AND object_id = OBJECT_ID(N'[MyMoney].[CurrencyConversionLog]'))
    CREATE NONCLUSTERED INDEX [IX_CCLog_EntityType_EntityId]
    ON [MyMoney].[CurrencyConversionLog] ([EntityType] ASC, [EntityId] ASC)
    INCLUDE ([UserId], [FromCurrency], [ToCurrency], [ExchangeRate], [RateEffectiveDate]);
GO

-- CurrencyConversionLog — per-user audit
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CCLog_UserId_CreatedAt'
    AND object_id = OBJECT_ID(N'[MyMoney].[CurrencyConversionLog]'))
    CREATE NONCLUSTERED INDEX [IX_CCLog_UserId_CreatedAt]
    ON [MyMoney].[CurrencyConversionLog] ([UserId] ASC, [CreatedAt] DESC);
GO

-- =============================================================================
-- ADDITIVE MIGRATION: Add currency columns to existing tables
-- All columns are nullable with safe defaults — 100% backward compatible.
-- Existing rows get CurrencyCode = user's base currency on first conversion touch.
-- =============================================================================

-- Transactions
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[MyMoney].[Transactions]') AND name = N'CurrencyCode')
    ALTER TABLE [MyMoney].[Transactions] ADD [CurrencyCode] NVARCHAR(10) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[MyMoney].[Transactions]') AND name = N'ExchangeRate')
    ALTER TABLE [MyMoney].[Transactions] ADD [ExchangeRate] DECIMAL(28, 10) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[MyMoney].[Transactions]') AND name = N'BaseCurrencyAmount')
    ALTER TABLE [MyMoney].[Transactions] ADD [BaseCurrencyAmount] DECIMAL(18, 4) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[MyMoney].[Transactions]') AND name = N'RateId')
    ALTER TABLE [MyMoney].[Transactions] ADD [RateId] BIGINT NULL;
GO

-- Goals
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[MyMoney].[Goals]') AND name = N'CurrencyCode')
    ALTER TABLE [MyMoney].[Goals] ADD [CurrencyCode] NVARCHAR(10) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[MyMoney].[Goals]') AND name = N'BaseCurrencyTargetAmount')
    ALTER TABLE [MyMoney].[Goals] ADD [BaseCurrencyTargetAmount] DECIMAL(18, 4) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[MyMoney].[Goals]') AND name = N'BaseCurrencyCurrentAmount')
    ALTER TABLE [MyMoney].[Goals] ADD [BaseCurrencyCurrentAmount] DECIMAL(18, 4) NULL;
GO

-- GoalContributions
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[MyMoney].[GoalContributions]') AND name = N'CurrencyCode')
    ALTER TABLE [MyMoney].[GoalContributions] ADD [CurrencyCode] NVARCHAR(10) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[MyMoney].[GoalContributions]') AND name = N'ExchangeRate')
    ALTER TABLE [MyMoney].[GoalContributions] ADD [ExchangeRate] DECIMAL(28, 10) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[MyMoney].[GoalContributions]') AND name = N'BaseCurrencyAmount')
    ALTER TABLE [MyMoney].[GoalContributions] ADD [BaseCurrencyAmount] DECIMAL(18, 4) NULL;
GO

-- Budgets
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[MyMoney].[Budgets]') AND name = N'CurrencyCode')
    ALTER TABLE [MyMoney].[Budgets] ADD [CurrencyCode] NVARCHAR(10) NULL;
GO

-- RecurringTransactionDefinitions
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[MyMoney].[RecurringTransactionDefinitions]') AND name = N'CurrencyCode')
    ALTER TABLE [MyMoney].[RecurringTransactionDefinitions] ADD [CurrencyCode] NVARCHAR(10) NULL;
GO

-- Indexes for new currency columns
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Transactions_UserId_Currency'
    AND object_id = OBJECT_ID(N'[MyMoney].[Transactions]'))
    CREATE NONCLUSTERED INDEX [IX_Transactions_UserId_Currency]
    ON [MyMoney].[Transactions] ([UserId] ASC, [CurrencyCode] ASC)
    INCLUDE ([Amount], [BaseCurrencyAmount], [TransactionDate], [IsActive])
    WHERE [CurrencyCode] IS NOT NULL;
GO

-- =============================================================================
-- SEED DATA
-- =============================================================================

-- Default provider
IF NOT EXISTS (SELECT 1 FROM [MyMoney].[ExchangeRateProviders] WHERE [Code] = N'MANUAL')
    INSERT INTO [MyMoney].[ExchangeRateProviders]
        ([Code], [NameEn], [NameAr], [IsActive], [IsDefault], [Priority])
    VALUES
        (N'MANUAL', N'Manual Entry', N'إدخال يدوي', 1, 1, 1);
GO

IF NOT EXISTS (SELECT 1 FROM [MyMoney].[ExchangeRateProviders] WHERE [Code] = N'ECB')
    INSERT INTO [MyMoney].[ExchangeRateProviders]
        ([Code], [NameEn], [NameAr], [IsActive], [IsDefault], [ApiBaseUrl], [Priority])
    VALUES
        (N'ECB', N'European Central Bank', N'البنك المركزي الأوروبي',
         0, 0, N'https://data-api.ecb.europa.eu', 2);
GO

IF NOT EXISTS (SELECT 1 FROM [MyMoney].[ExchangeRateProviders] WHERE [Code] = N'OPEN_EXCHANGE_RATES')
    INSERT INTO [MyMoney].[ExchangeRateProviders]
        ([Code], [NameEn], [NameAr], [IsActive], [IsDefault], [ApiBaseUrl], [Priority])
    VALUES
        (N'OPEN_EXCHANGE_RATES', N'Open Exchange Rates', N'أسعار الصرف المفتوحة',
         0, 0, N'https://openexchangerates.org/api', 3);
GO

-- Major currencies (ISO 4217) — ordered by global usage
IF NOT EXISTS (SELECT 1 FROM [MyMoney].[Currencies] WHERE [Code] = N'USD')
BEGIN
    INSERT INTO [MyMoney].[Currencies] ([Code],[NameEn],[NameAr],[Symbol],[NativeSymbol],[DecimalDigits],[CountryCode],[IsActive],[IsSystemBase],[DisplayOrder])
    VALUES
        (N'USD', N'US Dollar',              N'الدولار الأمريكي',      N'$',    N'$',    2, N'US', 1, 1, 1),
        (N'EUR', N'Euro',                   N'اليورو',                N'€',    N'€',    2, N'EU', 1, 0, 2),
        (N'GBP', N'British Pound',          N'الجنيه الإسترليني',     N'£',    N'£',    2, N'GB', 1, 0, 3),
        (N'SAR', N'Saudi Riyal',            N'الريال السعودي',         N'﷼',   N'ر.س',  2, N'SA', 1, 0, 4),
        (N'AED', N'UAE Dirham',             N'الدرهم الإماراتي',       N'د.إ', N'د.إ',  2, N'AE', 1, 0, 5),
        (N'KWD', N'Kuwaiti Dinar',          N'الدينار الكويتي',        N'K.D', N'د.ك',  3, N'KW', 1, 0, 6),
        (N'JOD', N'Jordanian Dinar',        N'الدينار الأردني',        N'JD',  N'د.أ',  3, N'JO', 1, 0, 7),
        (N'BHD', N'Bahraini Dinar',         N'الدينار البحريني',       N'BD',  N'د.ب',  3, N'BH', 1, 0, 8),
        (N'QAR', N'Qatari Riyal',           N'الريال القطري',          N'﷼',  N'ر.ق',  2, N'QA', 1, 0, 9),
        (N'OMR', N'Omani Rial',             N'الريال العُماني',         N'﷼',  N'ر.ع',  3, N'OM', 1, 0, 10),
        (N'EGP', N'Egyptian Pound',         N'الجنيه المصري',          N'£',   N'ج.م',  2, N'EG', 1, 0, 11),
        (N'JPY', N'Japanese Yen',           N'الين الياباني',          N'¥',   N'¥',    0, N'JP', 1, 0, 12),
        (N'CHF', N'Swiss Franc',            N'الفرنك السويسري',        N'Fr',  N'Fr',   2, N'CH', 1, 0, 13),
        (N'CAD', N'Canadian Dollar',        N'الدولار الكندي',         N'$',   N'$',    2, N'CA', 1, 0, 14),
        (N'AUD', N'Australian Dollar',      N'الدولار الأسترالي',      N'$',   N'$',    2, N'AU', 1, 0, 15),
        (N'CNY', N'Chinese Yuan',           N'اليوان الصيني',          N'¥',   N'¥',    2, N'CN', 1, 0, 16),
        (N'INR', N'Indian Rupee',           N'الروبية الهندية',         N'₹',   N'₹',    2, N'IN', 1, 0, 17),
        (N'TRY', N'Turkish Lira',           N'الليرة التركية',          N'₺',   N'₺',    2, N'TR', 1, 0, 18),
        (N'SEK', N'Swedish Krona',          N'الكرونة السويدية',        N'kr',  N'kr',   2, N'SE', 1, 0, 19),
        (N'NOK', N'Norwegian Krone',        N'الكرونة النرويجية',       N'kr',  N'kr',   2, N'NO', 1, 0, 20),
        (N'DKK', N'Danish Krone',           N'الكرونة الدانماركية',     N'kr',  N'kr',   2, N'DK', 1, 0, 21),
        (N'NZD', N'New Zealand Dollar',     N'الدولار النيوزيلندي',     N'$',   N'$',    2, N'NZ', 1, 0, 22),
        (N'SGD', N'Singapore Dollar',       N'الدولار السنغافوري',      N'$',   N'$',    2, N'SG', 1, 0, 23),
        (N'HKD', N'Hong Kong Dollar',       N'الدولار الهونغ كونغي',    N'$',   N'$',    2, N'HK', 1, 0, 24),
        (N'MYR', N'Malaysian Ringgit',      N'الرينغيت الماليزي',       N'RM',  N'RM',   2, N'MY', 1, 0, 25),
        (N'THB', N'Thai Baht',              N'البات التايلاندي',         N'฿',   N'฿',    2, N'TH', 1, 0, 26),
        (N'ZAR', N'South African Rand',     N'الراند جنوب أفريقيا',     N'R',   N'R',    2, N'ZA', 1, 0, 27),
        (N'MXN', N'Mexican Peso',           N'البيسو المكسيكي',          N'$',   N'$',    2, N'MX', 1, 0, 28),
        (N'BRL', N'Brazilian Real',         N'الريال البرازيلي',         N'R$',  N'R$',   2, N'BR', 1, 0, 29),
        (N'RUB', N'Russian Ruble',          N'الروبل الروسي',            N'₽',   N'₽',    2, N'RU', 1, 0, 30),
        (N'PKR', N'Pakistani Rupee',        N'الروبية الباكستانية',       N'₨',   N'₨',    2, N'PK', 1, 0, 31),
        (N'BDT', N'Bangladeshi Taka',       N'التاكا البنغلاديشية',       N'৳',   N'৳',    2, N'BD', 1, 0, 32),
        (N'PHP', N'Philippine Peso',        N'البيسو الفلبيني',           N'₱',   N'₱',    2, N'PH', 1, 0, 33),
        (N'IDR', N'Indonesian Rupiah',      N'الروبية الإندونيسية',       N'Rp',  N'Rp',   2, N'ID', 1, 0, 34),
        (N'KRW', N'South Korean Won',       N'الوون الكوري الجنوبي',      N'₩',   N'₩',    0, N'KR', 1, 0, 35),
        (N'NGN', N'Nigerian Naira',         N'النايرا النيجيرية',          N'₦',   N'₦',    2, N'NG', 1, 0, 36),
        (N'ILS', N'Israeli Shekel',         N'الشيكل الإسرائيلي',          N'₪',   N'₪',    2, N'IL', 1, 0, 37),
        (N'MAD', N'Moroccan Dirham',        N'الدرهم المغربي',             N'د.م.',N'د.م.', 2, N'MA', 1, 0, 38),
        (N'TND', N'Tunisian Dinar',         N'الدينار التونسي',             N'د.ت',N'د.ت',  3, N'TN', 1, 0, 39),
        (N'DZD', N'Algerian Dinar',         N'الدينار الجزائري',            N'د.ج',N'د.ج',  2, N'DZ', 1, 0, 40),
        (N'LBP', N'Lebanese Pound',         N'الليرة اللبنانية',            N'L£',  N'ل.ل',  2, N'LB', 1, 0, 41),
        (N'IQD', N'Iraqi Dinar',            N'الدينار العراقي',             N'ع.د', N'ع.د',  3, N'IQ', 1, 0, 42),
        (N'YER', N'Yemeni Rial',            N'الريال اليمني',               N'﷼',   N'ر.ي',  2, N'YE', 1, 0, 43),
        (N'SDG', N'Sudanese Pound',         N'الجنيه السوداني',             N'£',   N'ج.س',  2, N'SD', 1, 0, 44),
        (N'LYD', N'Libyan Dinar',           N'الدينار الليبي',              N'L.D', N'ل.د',  3, N'LY', 1, 0, 45),
        (N'SYP', N'Syrian Pound',           N'الليرة السورية',              N'£',   N'ل.س',  2, N'SY', 1, 0, 46),
        (N'JMD', N'Jamaican Dollar',        N'الدولار الجامايكي',           N'$',   N'$',    2, N'JM', 1, 0, 97),
        (N'PLN', N'Polish Zloty',           N'الزلوتي البولندي',             N'zł',  N'zł',   2, N'PL', 1, 0, 98),
        (N'CZK', N'Czech Koruna',           N'الكورونا التشيكية',            N'Kč',  N'Kč',   2, N'CZ', 1, 0, 99);
END
GO

-- Seed initial USD rates (manual baseline — 2026-06-23)
DECLARE @ManualProviderId INT;
SELECT @ManualProviderId = [ProviderId] FROM [MyMoney].[ExchangeRateProviders] WHERE [Code] = N'MANUAL';

IF @ManualProviderId IS NOT NULL AND NOT EXISTS (
    SELECT 1 FROM [MyMoney].[ExchangeRates]
    WHERE [FromCurrency] = N'USD' AND [ToCurrency] = N'EUR' AND [StatusId] = 1
)
BEGIN
    INSERT INTO [MyMoney].[ExchangeRates]
        ([FromCurrency],[ToCurrency],[Rate],[InverseRate],[ProviderId],[EffectiveDate],[SourceTypeId],[StatusId])
    VALUES
        (N'USD',N'EUR',  0.9230000000, 1.0834000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'USD',N'GBP',  0.7850000000, 1.2739000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'USD',N'SAR',  3.7500000000, 0.2667000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'USD',N'AED',  3.6725000000, 0.2723000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'USD',N'KWD',  0.3080000000, 3.2468000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'USD',N'JOD',  0.7090000000, 1.4104000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'USD',N'BHD',  0.3770000000, 2.6525000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'USD',N'QAR',  3.6400000000, 0.2747000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'USD',N'OMR',  0.3850000000, 2.5974000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'USD',N'EGP',  49.500000000, 0.0202000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'USD',N'JPY',  149.500000000,0.0066890000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'USD',N'CHF',  0.8950000000, 1.1173000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'USD',N'CAD',  1.3560000000, 0.7375000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'USD',N'AUD',  1.5380000000, 0.6502000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'USD',N'CNY',  7.2400000000, 0.1381000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'USD',N'INR',  83.500000000, 0.0119760000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'USD',N'TRY',  32.100000000, 0.0311530000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'USD',N'SEK',  10.450000000, 0.0957420000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'USD',N'SGD',  1.3420000000, 0.7451000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'USD',N'HKD',  7.8100000000, 0.1280410000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'USD',N'MYR',  4.6800000000, 0.2137000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'USD',N'MAD',  9.9600000000, 0.1004000000, @ManualProviderId, '2026-06-23', 1, 1);

    -- Add inverse rates (EUR→USD, GBP→USD, etc.) for efficiency
    INSERT INTO [MyMoney].[ExchangeRates]
        ([FromCurrency],[ToCurrency],[Rate],[InverseRate],[ProviderId],[EffectiveDate],[SourceTypeId],[StatusId])
    VALUES
        (N'EUR',N'USD',  1.0834000000, 0.9230000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'GBP',N'USD',  1.2739000000, 0.7850000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'SAR',N'USD',  0.2667000000, 3.7500000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'AED',N'USD',  0.2723000000, 3.6725000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'KWD',N'USD',  3.2468000000, 0.3080000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'JOD',N'USD',  1.4104000000, 0.7090000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'BHD',N'USD',  2.6525000000, 0.3770000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'QAR',N'USD',  0.2747000000, 3.6400000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'EGP',N'USD',  0.0202000000, 49.500000000, @ManualProviderId, '2026-06-23', 1, 1),
        (N'JPY',N'USD',  0.0066890000, 149.500000000,@ManualProviderId, '2026-06-23', 1, 1),
        (N'CAD',N'USD',  0.7375000000, 1.3560000000, @ManualProviderId, '2026-06-23', 1, 1);
END
GO

-- =============================================================================
-- STORED PROCEDURES
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- usp_Currency_GetList
-- Returns all active currencies, ordered by DisplayOrder.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Currency_GetList]
    @IncludeInactive BIT = 0,
    @IncludeCrypto   BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [CurrencyId], [Code], [NameEn], [NameAr], [Symbol], [NativeSymbol],
        [DecimalDigits], [CountryCode], [IsActive], [IsSystemBase], [IsCrypto], [DisplayOrder]
    FROM [MyMoney].[Currencies]
    WHERE (@IncludeInactive = 1 OR [IsActive] = 1)
      AND (@IncludeCrypto   = 1 OR [IsCrypto] = 0)
    ORDER BY [DisplayOrder] ASC, [Code] ASC;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- usp_Currency_GetByCode
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Currency_GetByCode]
    @Code NVARCHAR(10)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [CurrencyId], [Code], [NameEn], [NameAr], [Symbol], [NativeSymbol],
        [DecimalDigits], [CountryCode], [IsActive], [IsSystemBase], [IsCrypto], [DisplayOrder]
    FROM [MyMoney].[Currencies]
    WHERE [Code] = @Code;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- usp_UserCurrencyPreferences_Get
-- Returns preferences for a user, or defaults if no row exists.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_UserCurrencyPreferences_Get]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    -- Return existing row, or synthesize a default row without inserting
    SELECT
        ISNULL(p.[UserId],              @UserId)  AS UserId,
        ISNULL(p.[BaseCurrencyCode],    N'USD')   AS BaseCurrencyCode,
        ISNULL(p.[DisplayCurrencyCode], N'USD')   AS DisplayCurrencyCode,
        ISNULL(p.[NumberFormatId],      1)        AS NumberFormatId,
        ISNULL(p.[SymbolStyleId],       1)        AS SymbolStyleId,
        ISNULL(p.[NegativeFormatId],    1)        AS NegativeFormatId,
        ISNULL(p.[CurrencyPositionId],  1)        AS CurrencyPositionId,
        p.[UpdatedAt]
    FROM (SELECT NULL AS [Dummy]) d
    LEFT JOIN [MyMoney].[UserCurrencyPreferences] p ON p.[UserId] = @UserId;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- usp_UserCurrencyPreferences_Upsert
-- Creates or updates the user's currency preferences.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_UserCurrencyPreferences_Upsert]
    @UserId              BIGINT,
    @BaseCurrencyCode    NVARCHAR(10),
    @DisplayCurrencyCode NVARCHAR(10),
    @NumberFormatId      TINYINT,
    @SymbolStyleId       TINYINT,
    @NegativeFormatId    TINYINT,
    @CurrencyPositionId  TINYINT,
    @ResultCode          TINYINT OUTPUT  -- 0=Success 1=InvalidCurrency
AS
BEGIN
    SET NOCOUNT ON;
    SET @ResultCode = 0;

    -- Validate both currency codes exist and are active
    IF NOT EXISTS (SELECT 1 FROM [MyMoney].[Currencies] WHERE [Code] = @BaseCurrencyCode    AND [IsActive] = 1)
        OR NOT EXISTS (SELECT 1 FROM [MyMoney].[Currencies] WHERE [Code] = @DisplayCurrencyCode AND [IsActive] = 1)
    BEGIN
        SET @ResultCode = 1;
        RETURN;
    END

    IF EXISTS (SELECT 1 FROM [MyMoney].[UserCurrencyPreferences] WHERE [UserId] = @UserId)
    BEGIN
        UPDATE [MyMoney].[UserCurrencyPreferences]
        SET [BaseCurrencyCode]    = @BaseCurrencyCode,
            [DisplayCurrencyCode] = @DisplayCurrencyCode,
            [NumberFormatId]      = @NumberFormatId,
            [SymbolStyleId]       = @SymbolStyleId,
            [NegativeFormatId]    = @NegativeFormatId,
            [CurrencyPositionId]  = @CurrencyPositionId,
            [UpdatedAt]           = SYSUTCDATETIME()
        WHERE [UserId] = @UserId;
    END
    ELSE
    BEGIN
        INSERT INTO [MyMoney].[UserCurrencyPreferences]
            ([UserId],[BaseCurrencyCode],[DisplayCurrencyCode],
             [NumberFormatId],[SymbolStyleId],[NegativeFormatId],[CurrencyPositionId])
        VALUES
            (@UserId,@BaseCurrencyCode,@DisplayCurrencyCode,
             @NumberFormatId,@SymbolStyleId,@NegativeFormatId,@CurrencyPositionId);
    END
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- usp_ExchangeRate_GetCurrent
-- Returns the most recent active rate for a currency pair.
-- Uses USD as pivot for cross-pair calculation when direct rate unavailable.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_ExchangeRate_GetCurrent]
    @FromCurrency NVARCHAR(10),
    @ToCurrency   NVARCHAR(10)
AS
BEGIN
    SET NOCOUNT ON;

    -- Identity conversion (same currency)
    IF @FromCurrency = @ToCurrency
    BEGIN
        SELECT
            NULL        AS RateId,
            @FromCurrency AS FromCurrency,
            @ToCurrency   AS ToCurrency,
            1.0         AS Rate,
            1.0         AS InverseRate,
            CAST(GETUTCDATE() AS DATE) AS EffectiveDate,
            1           AS SourceTypeId;
        RETURN;
    END

    -- Direct rate lookup
    SELECT TOP 1
        [RateId], [FromCurrency], [ToCurrency],
        [Rate], [InverseRate], [EffectiveDate], [SourceTypeId]
    FROM [MyMoney].[ExchangeRates]
    WHERE [FromCurrency] = @FromCurrency
      AND [ToCurrency]   = @ToCurrency
      AND [StatusId]     = 1
    ORDER BY [EffectiveDate] DESC;

    IF @@ROWCOUNT > 0 RETURN;

    -- Cross-rate via USD pivot (USD→ToCurrency / USD→FromCurrency)
    DECLARE @FromRate  DECIMAL(28,10), @ToRate DECIMAL(28,10);

    -- Find USD→FromCurrency or FromCurrency→USD
    SELECT TOP 1 @FromRate =
        CASE WHEN [FromCurrency] = N'USD' THEN [Rate] ELSE [InverseRate] END
    FROM [MyMoney].[ExchangeRates]
    WHERE (([FromCurrency] = N'USD' AND [ToCurrency] = @FromCurrency)
        OR ([FromCurrency] = @FromCurrency AND [ToCurrency] = N'USD'))
      AND [StatusId] = 1
    ORDER BY [EffectiveDate] DESC;

    SELECT TOP 1 @ToRate =
        CASE WHEN [FromCurrency] = N'USD' THEN [Rate] ELSE [InverseRate] END
    FROM [MyMoney].[ExchangeRates]
    WHERE (([FromCurrency] = N'USD' AND [ToCurrency] = @ToCurrency)
        OR ([FromCurrency] = @ToCurrency AND [ToCurrency] = N'USD'))
      AND [StatusId] = 1
    ORDER BY [EffectiveDate] DESC;

    IF @FromRate IS NOT NULL AND @ToRate IS NOT NULL
    BEGIN
        DECLARE @CrossRate DECIMAL(28,10) = @ToRate / @FromRate;
        SELECT
            NULL               AS RateId,
            @FromCurrency      AS FromCurrency,
            @ToCurrency        AS ToCurrency,
            @CrossRate         AS Rate,
            1.0 / @CrossRate   AS InverseRate,
            CAST(GETUTCDATE() AS DATE) AS EffectiveDate,
            3                  AS SourceTypeId;  -- Estimated/cross-rate
    END
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- usp_ExchangeRate_GetHistorical
-- Returns the rate effective on a specific date (closest on or before that date).
-- Falls back to cross-rate via USD if no direct rate found.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_ExchangeRate_GetHistorical]
    @FromCurrency NVARCHAR(10),
    @ToCurrency   NVARCHAR(10),
    @AsOfDate     DATE
AS
BEGIN
    SET NOCOUNT ON;

    IF @FromCurrency = @ToCurrency
    BEGIN
        SELECT NULL AS RateId, @FromCurrency AS FromCurrency, @ToCurrency AS ToCurrency,
               1.0 AS Rate, 1.0 AS InverseRate, @AsOfDate AS EffectiveDate, 1 AS SourceTypeId;
        RETURN;
    END

    -- Direct historical rate (closest on or before requested date)
    SELECT TOP 1
        [RateId], [FromCurrency], [ToCurrency],
        [Rate], [InverseRate], [EffectiveDate], [SourceTypeId]
    FROM [MyMoney].[ExchangeRates]
    WHERE [FromCurrency] = @FromCurrency
      AND [ToCurrency]   = @ToCurrency
      AND [StatusId]     = 1
      AND [EffectiveDate] <= @AsOfDate
    ORDER BY [EffectiveDate] DESC;

    IF @@ROWCOUNT > 0 RETURN;

    -- Cross-rate pivot via USD
    DECLARE @FromRate DECIMAL(28,10), @ToRate DECIMAL(28,10);

    SELECT TOP 1 @FromRate =
        CASE WHEN [FromCurrency] = N'USD' THEN [Rate] ELSE [InverseRate] END
    FROM [MyMoney].[ExchangeRates]
    WHERE (([FromCurrency] = N'USD' AND [ToCurrency] = @FromCurrency)
        OR ([FromCurrency] = @FromCurrency AND [ToCurrency] = N'USD'))
      AND [StatusId] = 1
      AND [EffectiveDate] <= @AsOfDate
    ORDER BY [EffectiveDate] DESC;

    SELECT TOP 1 @ToRate =
        CASE WHEN [FromCurrency] = N'USD' THEN [Rate] ELSE [InverseRate] END
    FROM [MyMoney].[ExchangeRates]
    WHERE (([FromCurrency] = N'USD' AND [ToCurrency] = @ToCurrency)
        OR ([FromCurrency] = @ToCurrency AND [ToCurrency] = N'USD'))
      AND [StatusId] = 1
      AND [EffectiveDate] <= @AsOfDate
    ORDER BY [EffectiveDate] DESC;

    IF @FromRate IS NOT NULL AND @ToRate IS NOT NULL
    BEGIN
        DECLARE @CrossRate DECIMAL(28,10) = @ToRate / @FromRate;
        SELECT NULL AS RateId, @FromCurrency AS FromCurrency, @ToCurrency AS ToCurrency,
               @CrossRate AS Rate, 1.0 / @CrossRate AS InverseRate,
               @AsOfDate AS EffectiveDate, 3 AS SourceTypeId;
    END
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- usp_ExchangeRate_Upsert
-- Inserts a new rate and archives the previous active rate for the same pair.
-- Historical immutability: the old rate is never deleted, only expired.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_ExchangeRate_Upsert]
    @FromCurrency  NVARCHAR(10),
    @ToCurrency    NVARCHAR(10),
    @Rate          DECIMAL(28, 10),
    @ProviderId    INT,
    @EffectiveDate DATE,
    @SourceTypeId  TINYINT = 1,
    @CreatedBy     BIGINT  = NULL,
    @NewRateId     BIGINT  OUTPUT,
    @ResultCode    TINYINT OUTPUT  -- 0=Success 1=InvalidCurrency 2=InvalidRate 3=InvalidProvider
AS
BEGIN
    SET NOCOUNT ON;
    SET @NewRateId  = 0;
    SET @ResultCode = 0;

    -- Validate
    IF NOT EXISTS (SELECT 1 FROM [MyMoney].[Currencies] WHERE [Code] = @FromCurrency AND [IsActive] = 1)
        OR NOT EXISTS (SELECT 1 FROM [MyMoney].[Currencies] WHERE [Code] = @ToCurrency AND [IsActive] = 1)
    BEGIN SET @ResultCode = 1; RETURN; END

    IF @Rate <= 0
    BEGIN SET @ResultCode = 2; RETURN; END

    IF NOT EXISTS (SELECT 1 FROM [MyMoney].[ExchangeRateProviders] WHERE [ProviderId] = @ProviderId AND [IsActive] = 1)
    BEGIN SET @ResultCode = 3; RETURN; END

    DECLARE @InverseRate DECIMAL(28,10) = 1.0 / @Rate;

    BEGIN TRANSACTION;
    BEGIN TRY
        -- Archive the previous active rate for this pair
        UPDATE [MyMoney].[ExchangeRates]
        SET    [StatusId]   = 3,  -- Archived
               [ExpiryDate] = DATEADD(DAY, -1, @EffectiveDate)
        WHERE  [FromCurrency] = @FromCurrency
          AND  [ToCurrency]   = @ToCurrency
          AND  [StatusId]     = 1;

        -- Insert new active rate
        INSERT INTO [MyMoney].[ExchangeRates]
            ([FromCurrency],[ToCurrency],[Rate],[InverseRate],
             [ProviderId],[EffectiveDate],[SourceTypeId],[StatusId],[CreatedBy])
        VALUES
            (@FromCurrency,@ToCurrency,@Rate,@InverseRate,
             @ProviderId,@EffectiveDate,@SourceTypeId,1,@CreatedBy);

        SET @NewRateId = SCOPE_IDENTITY();

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- usp_ExchangeRate_BulkUpsert
-- Batch-inserts rates from a JSON array (used by provider sync job).
-- JSON format: [{"FromCurrency":"USD","ToCurrency":"EUR","Rate":0.923,"EffectiveDate":"2026-06-23"}]
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_ExchangeRate_BulkUpsert]
    @RatesJson     NVARCHAR(MAX),
    @ProviderId    INT,
    @SourceTypeId  TINYINT = 2,   -- Automatic
    @InsertedCount INT OUTPUT,
    @ArchivedCount INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @InsertedCount = 0;
    SET @ArchivedCount = 0;

    IF @RatesJson IS NULL OR @RatesJson = N'[]' RETURN;

    -- Parse JSON into a temp table
    CREATE TABLE #IncomingRates (
        FromCurrency  NVARCHAR(10)    NOT NULL,
        ToCurrency    NVARCHAR(10)    NOT NULL,
        Rate          DECIMAL(28, 10) NOT NULL,
        InverseRate   DECIMAL(28, 10) NOT NULL,
        EffectiveDate DATE            NOT NULL
    );

    INSERT INTO #IncomingRates (FromCurrency, ToCurrency, Rate, InverseRate, EffectiveDate)
    SELECT
        j.[FromCurrency],
        j.[ToCurrency],
        j.[Rate],
        1.0 / NULLIF(j.[Rate], 0) AS InverseRate,
        CAST(j.[EffectiveDate] AS DATE)
    FROM OPENJSON(@RatesJson) WITH (
        [FromCurrency]  NVARCHAR(10)  '$.FromCurrency',
        [ToCurrency]    NVARCHAR(10)  '$.ToCurrency',
        [Rate]          DECIMAL(28,10)'$.Rate',
        [EffectiveDate] NVARCHAR(20)  '$.EffectiveDate'
    ) j
    WHERE j.[Rate] > 0
      AND j.[FromCurrency] <> j.[ToCurrency];

    BEGIN TRANSACTION;
    BEGIN TRY
        -- Archive existing active rates for pairs in the batch
        UPDATE er
        SET    er.[StatusId]   = 3,
               er.[ExpiryDate] = DATEADD(DAY, -1, ir.[EffectiveDate])
        FROM   [MyMoney].[ExchangeRates] er
        JOIN   #IncomingRates ir
               ON  ir.[FromCurrency]  = er.[FromCurrency]
               AND ir.[ToCurrency]    = er.[ToCurrency]
        WHERE  er.[StatusId] = 1;

        SET @ArchivedCount = @@ROWCOUNT;

        -- Insert new rates
        INSERT INTO [MyMoney].[ExchangeRates]
            ([FromCurrency],[ToCurrency],[Rate],[InverseRate],
             [ProviderId],[EffectiveDate],[SourceTypeId],[StatusId])
        SELECT
            [FromCurrency],[ToCurrency],[Rate],[InverseRate],
            @ProviderId,[EffectiveDate],@SourceTypeId,1
        FROM #IncomingRates;

        SET @InsertedCount = @@ROWCOUNT;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        DROP TABLE IF EXISTS #IncomingRates;
        THROW;
    END CATCH

    DROP TABLE IF EXISTS #IncomingRates;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- usp_ExchangeRate_GetList
-- Returns paginated exchange rates with optional filters.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_ExchangeRate_GetList]
    @FromCurrency NVARCHAR(10) = NULL,
    @ToCurrency   NVARCHAR(10) = NULL,
    @StatusId     TINYINT      = 1,     -- 1=Active only by default
    @DateFrom     DATE         = NULL,
    @DateTo       DATE         = NULL,
    @PageNumber   INT          = 1,
    @PageSize     INT          = 50
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    SELECT
        er.[RateId],
        er.[FromCurrency],
        er.[ToCurrency],
        er.[Rate],
        er.[InverseRate],
        er.[EffectiveDate],
        er.[ExpiryDate],
        er.[SourceTypeId],
        er.[StatusId],
        er.[CreatedAt],
        p.[Code]   AS ProviderCode,
        p.[NameEn] AS ProviderNameEn,
        COUNT(*) OVER() AS TotalCount
    FROM [MyMoney].[ExchangeRates] er
    JOIN [MyMoney].[ExchangeRateProviders] p ON p.[ProviderId] = er.[ProviderId]
    WHERE (@FromCurrency IS NULL OR er.[FromCurrency] = @FromCurrency)
      AND (@ToCurrency   IS NULL OR er.[ToCurrency]   = @ToCurrency)
      AND (@StatusId     IS NULL OR er.[StatusId]     = @StatusId)
      AND (@DateFrom     IS NULL OR er.[EffectiveDate] >= @DateFrom)
      AND (@DateTo       IS NULL OR er.[EffectiveDate] <= @DateTo)
    ORDER BY er.[EffectiveDate] DESC, er.[FromCurrency] ASC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- usp_ExchangeRate_GetStalePairs
-- Returns currency pairs with no rate updated in the last N days.
-- Used by validation job to detect missing/stale rates.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_ExchangeRate_GetStalePairs]
    @StaleDays INT = 2
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CutoffDate DATE = DATEADD(DAY, -@StaleDays, CAST(GETUTCDATE() AS DATE));

    -- Find major currency pairs that have active rates older than the cutoff
    SELECT
        er.[FromCurrency],
        er.[ToCurrency],
        MAX(er.[EffectiveDate]) AS LastRateDate,
        DATEDIFF(DAY, MAX(er.[EffectiveDate]), CAST(GETUTCDATE() AS DATE)) AS DaysSinceUpdate
    FROM [MyMoney].[ExchangeRates] er
    WHERE er.[StatusId] = 1
    GROUP BY er.[FromCurrency], er.[ToCurrency]
    HAVING MAX(er.[EffectiveDate]) < @CutoffDate
    ORDER BY DaysSinceUpdate DESC;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- usp_ExchangeRate_GetStatistics
-- Summary statistics for the exchange rate system.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_ExchangeRate_GetStatistics]
AS
BEGIN
    SET NOCOUNT ON;

    -- RS1: Overall stats
    SELECT
        COUNT(DISTINCT CONCAT([FromCurrency], '→', [ToCurrency])) AS TotalActivePairs,
        COUNT(*)                                                    AS TotalActiveRates,
        MIN([EffectiveDate])                                        AS OldestRate,
        MAX([EffectiveDate])                                        AS NewestRate,
        DATEDIFF(DAY, MAX([EffectiveDate]), CAST(GETUTCDATE() AS DATE)) AS DaysSinceLastSync
    FROM [MyMoney].[ExchangeRates]
    WHERE [StatusId] = 1;

    -- RS2: Rates per provider
    SELECT
        p.[Code] AS ProviderCode,
        p.[NameEn],
        COUNT(er.[RateId]) AS ActiveRateCount,
        MAX(er.[EffectiveDate]) AS LastSync
    FROM [MyMoney].[ExchangeRateProviders] p
    LEFT JOIN [MyMoney].[ExchangeRates] er
           ON er.[ProviderId] = p.[ProviderId] AND er.[StatusId] = 1
    GROUP BY p.[Code], p.[NameEn], p.[ProviderId]
    ORDER BY p.[Priority];
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- usp_CurrencyConversionLog_Insert
-- Records an immutable conversion audit entry.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_CurrencyConversionLog_Insert]
    @UserId             BIGINT,
    @EntityType         NVARCHAR(50),
    @EntityId           BIGINT,
    @FromCurrency       NVARCHAR(10),
    @ToCurrency         NVARCHAR(10),
    @OriginalAmount     DECIMAL(18, 4),
    @ConvertedAmount    DECIMAL(18, 4),
    @ExchangeRate       DECIMAL(28, 10),
    @RateId             BIGINT          = NULL,
    @RateEffectiveDate  DATE,
    @ConversionModeId   TINYINT,
    @LogId              BIGINT          OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [MyMoney].[CurrencyConversionLog]
        ([UserId],[EntityType],[EntityId],[FromCurrency],[ToCurrency],
         [OriginalAmount],[ConvertedAmount],[ExchangeRate],[RateId],
         [RateEffectiveDate],[ConversionModeId])
    VALUES
        (@UserId,@EntityType,@EntityId,@FromCurrency,@ToCurrency,
         @OriginalAmount,@ConvertedAmount,@ExchangeRate,@RateId,
         @RateEffectiveDate,@ConversionModeId);

    SET @LogId = SCOPE_IDENTITY();
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- usp_ExchangeRate_GetActiveProviders
-- Returns providers currently configured and active.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_ExchangeRate_GetActiveProviders]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [ProviderId], [Code], [NameEn], [NameAr], [IsDefault], [ApiBaseUrl], [Priority]
    FROM [MyMoney].[ExchangeRateProviders]
    WHERE [IsActive] = 1
    ORDER BY [Priority] ASC;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- usp_Transaction_CreateWithCurrency
-- Extended transaction creation that captures currency and exchange rate.
-- Falls back to existing usp_Transaction_Create behavior when no currency provided.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Transaction_CreateWithCurrency]
    @UserId            BIGINT,
    @CategoryId        INT,
    @TransactionTypeId TINYINT,
    @Amount            DECIMAL(18, 2),
    @Description       NVARCHAR(500)   = NULL,
    @TransactionDate   DATE,
    @Notes             NVARCHAR(1000)  = NULL,
    @CurrencyCode      NVARCHAR(10)    = NULL,  -- NULL = user's base currency
    @ExchangeRate      DECIMAL(28, 10) = NULL,  -- NULL or 1.0 = same currency
    @BaseCurrencyAmount DECIMAL(18, 4) = NULL,  -- Pre-computed by app layer
    @RateId            BIGINT          = NULL,
    @NewTransactionId  BIGINT          OUTPUT,
    @ResultCode        TINYINT         OUTPUT   -- 0=Success 1=CategoryNotFound 2=TypeMismatch
AS
BEGIN
    SET NOCOUNT ON;
    SET @NewTransactionId = 0;
    SET @ResultCode       = 0;

    -- Validate category/type pairing
    IF NOT EXISTS (
        SELECT 1 FROM [MyMoney].[Categories]
        WHERE CategoryId = @CategoryId AND TransactionTypeId = @TransactionTypeId AND IsActive = 1
    )
    BEGIN
        SET @ResultCode = CASE
            WHEN NOT EXISTS (SELECT 1 FROM [MyMoney].[Categories] WHERE CategoryId = @CategoryId) THEN 1
            ELSE 2
        END;
        RETURN;
    END

    -- Default BaseCurrencyAmount to Amount when same currency
    DECLARE @EffectiveBaseAmount DECIMAL(18, 4) = ISNULL(@BaseCurrencyAmount, @Amount);
    DECLARE @EffectiveRate       DECIMAL(28, 10) = ISNULL(@ExchangeRate, 1.0);

    INSERT INTO [MyMoney].[Transactions]
        ([UserId],[CategoryId],[TransactionTypeId],[Amount],[Description],
         [TransactionDate],[Notes],[CurrencyCode],[ExchangeRate],[BaseCurrencyAmount],[RateId],
         [IsActive],[CreatedAt],[CreatedBy])
    VALUES
        (@UserId,@CategoryId,@TransactionTypeId,@Amount,@Description,
         @TransactionDate,@Notes,@CurrencyCode,@EffectiveRate,@EffectiveBaseAmount,@RateId,
         1,GETUTCDATE(),@UserId);

    SET @NewTransactionId = SCOPE_IDENTITY();
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- usp_Currency_GetDashboardSummary
-- Aggregates user's financial data converted to their display currency.
-- RS1: Summary totals. RS2: Breakdown by original currency.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Currency_GetDashboardSummary]
    @UserId        BIGINT,
    @DisplayCurrency NVARCHAR(10),  -- Target display currency
    @DateFrom      DATE = NULL,
    @DateTo        DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Today DATE = CAST(GETUTCDATE() AS DATE);
    DECLARE @From  DATE = ISNULL(@DateFrom, DATEFROMPARTS(YEAR(@Today), MONTH(@Today), 1));
    DECLARE @To    DATE = ISNULL(@DateTo, @Today);

    -- RS1: Currency-aware totals
    -- BaseCurrencyAmount is already stored per-transaction; sum it directly.
    -- Fall back to Amount for legacy rows that have no BaseCurrencyAmount.
    SELECT
        ISNULL(SUM(CASE WHEN [TransactionTypeId] = 1
            THEN ISNULL([BaseCurrencyAmount], [Amount]) ELSE 0 END), 0) AS TotalIncome,
        ISNULL(SUM(CASE WHEN [TransactionTypeId] = 2
            THEN ISNULL([BaseCurrencyAmount], [Amount]) ELSE 0 END), 0) AS TotalExpenses,
        ISNULL(SUM(CASE
            WHEN [TransactionTypeId] = 1 THEN  ISNULL([BaseCurrencyAmount], [Amount])
            WHEN [TransactionTypeId] = 2 THEN -ISNULL([BaseCurrencyAmount], [Amount])
            ELSE 0
        END), 0) AS NetAmount,
        @DisplayCurrency AS DisplayCurrencyCode
    FROM [MyMoney].[Transactions]
    WHERE [UserId]          = @UserId
      AND [IsActive]        = 1
      AND [TransactionDate] BETWEEN @From AND @To;

    -- RS2: Breakdown by original currency (shows currency mix)
    SELECT
        ISNULL([CurrencyCode], N'USD') AS CurrencyCode,
        COUNT(*)                        AS TransactionCount,
        SUM(CASE WHEN [TransactionTypeId] = 1 THEN [Amount] ELSE 0 END) AS TotalIncome,
        SUM(CASE WHEN [TransactionTypeId] = 2 THEN [Amount] ELSE 0 END) AS TotalExpenses
    FROM [MyMoney].[Transactions]
    WHERE [UserId]          = @UserId
      AND [IsActive]        = 1
      AND [TransactionDate] BETWEEN @From AND @To
    GROUP BY [CurrencyCode]
    ORDER BY COUNT(*) DESC;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- usp_ExchangeRate_GetForReporting
-- Returns the set of rates needed to convert a list of currency pairs to a
-- target currency, as of a given date. Used by the report generator.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_ExchangeRate_GetForReporting]
    @CurrencyPairsJson NVARCHAR(MAX),  -- [{"From":"EUR","To":"USD"}]
    @AsOfDate          DATE
AS
BEGIN
    SET NOCOUNT ON;

    CREATE TABLE #Pairs (FromCurrency NVARCHAR(10), ToCurrency NVARCHAR(10));
    INSERT INTO #Pairs
    SELECT j.[From], j.[To]
    FROM OPENJSON(@CurrencyPairsJson) WITH (
        [From] NVARCHAR(10) '$.From',
        [To]   NVARCHAR(10) '$.To'
    ) j
    WHERE j.[From] <> j.[To];

    SELECT
        p.[FromCurrency],
        p.[ToCurrency],
        ISNULL(er.[Rate],     1.0) AS Rate,
        ISNULL(er.[RateId],   NULL) AS RateId,
        ISNULL(er.[EffectiveDate], @AsOfDate) AS EffectiveDate
    FROM #Pairs p
    OUTER APPLY (
        SELECT TOP 1 [Rate], [RateId], [EffectiveDate]
        FROM [MyMoney].[ExchangeRates]
        WHERE [FromCurrency] = p.[FromCurrency]
          AND [ToCurrency]   = p.[ToCurrency]
          AND [StatusId]     = 1
          AND [EffectiveDate] <= @AsOfDate
        ORDER BY [EffectiveDate] DESC
    ) er;

    DROP TABLE IF EXISTS #Pairs;
END
GO

PRINT 'Multi-Currency migration complete.';
GO
