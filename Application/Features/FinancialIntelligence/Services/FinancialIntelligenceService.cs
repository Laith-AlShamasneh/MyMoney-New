using Application.Common.Constants;
using Application.Features.FinancialIntelligence.DbModels;
using Application.Features.FinancialIntelligence.DTOs;
using Application.Features.FinancialIntelligence.Rules;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Shared.Constants;
using Shared.Enums.Intelligence;
using Shared.Enums.System;
using Shared.Results;

namespace Application.Features.FinancialIntelligence.Services;

internal sealed class FinancialIntelligenceService(
    IFinancialIntelligenceRepository filRepository,
    INotificationPublisher           notificationPublisher,
    IUserContext                     userContext,
    IMessageProvider                 messageProvider,
    IFinancialRulesEngine            rulesEngine) : IFinancialIntelligenceService
{
    private bool IsArabic => userContext.Language == SystemLanguages.Arabic;

    // ═════════════════════════════════════════════════════════════════════════
    // API-facing methods
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<ServiceResult<InsightListResponse>> GetInsightsAsync(
        GetInsightsRequest request,
        CancellationToken  ct = default)
    {
        var model = new GetInsightsDbModel
        {
            UserId     = userContext.UserId,
            IsRead     = request.IsRead,
            PageNumber = request.PageNumber,
            PageSize   = request.PageSize
        };

        var db       = await filRepository.GetInsightsAsync(model, ct);
        var isArabic = IsArabic;

        var items = db.Items.Select(r => MapInsight(r, isArabic)).ToList();
        var msg   = await messageProvider.GetMessagesAsync(MessageKeys.FinancialIntelligence.InsightsLoaded, ct);

        return ServiceResultFactory.Success(
            new InsightListResponse(items, db.TotalCount, db.UnreadCount, request.PageNumber, request.PageSize),
            InternalResponseCodes.OK,
            msg);
    }

    public async Task<ServiceResult<object?>> MarkInsightReadAsync(
        MarkInsightReadRequest request,
        CancellationToken      ct = default)
    {
        var affected = await filRepository.MarkInsightReadAsync(userContext.UserId, request.InsightId, ct);

        if (affected == 0)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.FinancialIntelligence.InsightNotFound, ct));
        }

        return ServiceResultFactory.Success<object?>(
            null,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.FinancialIntelligence.InsightMarkedRead, ct));
    }

    public async Task<ServiceResult<IReadOnlyList<PatternResponse>>> GetPatternsAsync(CancellationToken ct = default)
    {
        var db       = await filRepository.GetPatternsAsync(userContext.UserId, ct);
        var isArabic = IsArabic;

        var items = (IReadOnlyList<PatternResponse>)db.Select(p => MapPattern(p, isArabic)).ToList();
        var msg   = await messageProvider.GetMessagesAsync(MessageKeys.FinancialIntelligence.PatternsLoaded, ct);

        return ServiceResultFactory.Success(items, InternalResponseCodes.OK, msg);
    }

    public async Task<ServiceResult<RecommendationListResponse>> GetRecommendationsAsync(
        GetRecommendationsRequest request,
        CancellationToken         ct = default)
    {
        var model = new GetRecommendationsDbModel
        {
            UserId     = userContext.UserId,
            PageNumber = request.PageNumber,
            PageSize   = request.PageSize
        };

        var db       = await filRepository.GetRecommendationsAsync(model, ct);
        var isArabic = IsArabic;

        var items = db.Items.Select(r => MapRecommendation(r, isArabic)).ToList();
        var msg   = await messageProvider.GetMessagesAsync(MessageKeys.FinancialIntelligence.RecommendationsLoaded, ct);

        return ServiceResultFactory.Success(
            new RecommendationListResponse(items, db.TotalCount, request.PageNumber, request.PageSize),
            InternalResponseCodes.OK,
            msg);
    }

    public async Task<ServiceResult<object?>> MarkRecommendationAppliedAsync(
        MarkRecommendationAppliedRequest request,
        CancellationToken                ct = default)
    {
        var affected = await filRepository.MarkRecommendationAppliedAsync(userContext.UserId, request.RecommendationId, ct);

        if (affected == 0)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.FinancialIntelligence.RecommendationNotFound, ct));
        }

        return ServiceResultFactory.Success<object?>(
            null,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.FinancialIntelligence.RecommendationApplied, ct));
    }

    public async Task<ServiceResult<object?>> DismissRecommendationAsync(
        DismissRecommendationRequest request,
        CancellationToken            ct = default)
    {
        var affected = await filRepository.DismissRecommendationAsync(userContext.UserId, request.RecommendationId, ct);

        if (affected == 0)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.FinancialIntelligence.RecommendationNotFound, ct));
        }

        return ServiceResultFactory.Success<object?>(
            null,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.FinancialIntelligence.RecommendationDismissed, ct));
    }

    public async Task<ServiceResult<FILDashboardResponse>> GetDashboardAsync(CancellationToken ct = default)
    {
        var userId   = userContext.UserId;
        var isArabic = IsArabic;

        // Load all dashboard data in parallel — each is an independent read.
        var snapshotTask     = filRepository.GetLatestSnapshotAsync(userId, ct);
        var insightsTask     = filRepository.GetInsightsAsync(new GetInsightsDbModel { UserId = userId, IsRead = false, PageNumber = 1, PageSize = 5 }, ct);
        var patternsTask     = filRepository.GetPatternsAsync(userId, ct);
        var recommendTask    = filRepository.GetRecommendationsAsync(new GetRecommendationsDbModel { UserId = userId, PageNumber = 1, PageSize = 5 }, ct);

        await Task.WhenAll(snapshotTask, insightsTask, patternsTask, recommendTask);

        var snapshot     = snapshotTask.Result;
        var latestMonth  = snapshot is not null
            ? await filRepository.GetCategoryAnalyticsAsync(userId, snapshot.SnapshotDate.Year, snapshot.SnapshotDate.Month, ct)
            : [];

        var response = new FILDashboardResponse(
            LatestSnapshot:  snapshot is not null ? MapSnapshot(snapshot) : null,
            TopInsights:     insightsTask.Result.Items.Select(r => MapInsight(r, isArabic)).ToList(),
            Patterns:        patternsTask.Result.Select(p => MapPattern(p, isArabic)).ToList(),
            Recommendations: recommendTask.Result.Items.Select(r => MapRecommendation(r, isArabic)).ToList(),
            CategoryTrends:  latestMonth.Select(c => MapCategoryAnalytics(c, isArabic)).ToList()
        );

        var msg = await messageProvider.GetMessagesAsync(MessageKeys.FinancialIntelligence.DashboardLoaded, ct);
        return ServiceResultFactory.Success(response, InternalResponseCodes.OK, msg);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Background processing methods (called by job handlers)
    // ═════════════════════════════════════════════════════════════════════════

    public async Task ProcessDailyAsync(int year, int month, int day, CancellationToken ct = default)
    {
        var activeUsers = await filRepository.GetActiveUsersAsync(activeDays: 30, ct);

        foreach (var user in activeUsers)
        {
            try
            {
                await ProcessUserDailyAsync(user.UserId, year, month, ct);
            }
            catch (Exception)
            {
                // Isolate per-user failures: one user's error must not stop others.
            }
        }

        await filRepository.CleanupExpiredInsightsAsync(ct);
    }

    public async Task ProcessMonthlyAsync(int year, int month, CancellationToken ct = default)
    {
        var activeUsers = await filRepository.GetActiveUsersAsync(activeDays: 90, ct);

        foreach (var user in activeUsers)
        {
            try
            {
                await ProcessUserMonthlyAsync(user.UserId, year, month, ct);
            }
            catch (Exception)
            {
                // Isolate per-user failures.
            }
        }
    }

    public Task ProcessHourlyAnomalyAsync(DateTime fromUtc, CancellationToken ct = default) =>
        ProcessAnomalyWindowAsync(fromUtc, ct);

    private async Task ProcessAnomalyWindowAsync(DateTime fromUtc, CancellationToken ct)
    {
        var largeTx = await filRepository.GetRecentLargeTransactionsAsync(fromUtc, ct);

        foreach (var tx in largeTx)
        {
            var alreadyExists = await filRepository.InsightExistsForMonthAsync(
                tx.UserId, InsightCodes.UnusualTransaction,
                fromUtc.Year, fromUtc.Month, ct);

            if (alreadyExists) continue;

            var multiple = tx.UserAverage > 0
                ? Math.Round(tx.Amount / tx.UserAverage, 1)
                : 0;

            var dbModel = new CreateInsightDbModel
            {
                UserId        = tx.UserId,
                Type          = (byte)InsightType.Warning,
                Code          = InsightCodes.UnusualTransaction,
                Severity      = (byte)InsightSeverity.High,
                TitleEn       = "Unusual Transaction Detected",
                TitleAr       = "تم اكتشاف معاملة غير عادية",
                DescriptionEn = $"A transaction of {tx.Amount:N2} was recorded — {multiple}× your average transaction value.",
                DescriptionAr = $"تم تسجيل معاملة بقيمة {tx.Amount:N2} — تساوي {multiple}× متوسط معاملاتك.",
                DataPointJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    transactionId = tx.TransactionId,
                    amount        = tx.Amount,
                    userAverage   = tx.UserAverage,
                    multiple
                }),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
            };

            await filRepository.CreateInsightAsync(dbModel, ct);

            await notificationPublisher.PublishAsync(
                NotificationCodes.FILUnusualTransaction,
                tx.UserId,
                parameters: new Dictionary<string, string>
                {
                    { "Amount",   tx.Amount.ToString("N2") },
                    { "Multiple", multiple.ToString("F1") }
                },
                payload: new { code = NotificationCodes.FILUnusualTransaction },
                ct: ct);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Private helpers — per-user processing
    // ═════════════════════════════════════════════════════════════════════════

    private async Task ProcessUserDailyAsync(long userId, int year, int month, CancellationToken ct)
    {
        // Compute & upsert the current month's running snapshot.
        var computed = await filRepository.ComputeMonthlySnapshotAsync(
            new ComputeSnapshotDbModel { UserId = userId, Year = year, Month = month }, ct);

        if (computed is null || computed.TransactionCount == 0) return;

        await filRepository.UpsertSnapshotAsync(new UpsertSnapshotDbModel
        {
            UserId                  = userId,
            SnapshotDate            = new DateOnly(year, month, 1),
            PeriodType              = (byte)SnapshotPeriodType.Monthly,
            TotalIncome             = computed.TotalIncome,
            TotalExpense            = computed.TotalExpense,
            NetBalance              = computed.NetBalance,
            AverageDailySpend       = computed.AverageDailySpend,
            AverageTransactionValue = computed.AverageTransactionValue,
            TransactionCount        = computed.TransactionCount,
            TopCategoryId           = computed.TopCategoryId
        }, ct);
    }

    private async Task ProcessUserMonthlyAsync(long userId, int year, int month, CancellationToken ct)
    {
        // 1. Compute and upsert snapshot for the previous (completed) month.
        await ProcessUserDailyAsync(userId, year, month, ct);

        // 2. Compute and upsert category analytics.
        var categoryRows = await filRepository.ComputeCategoryAnalyticsAsync(
            new ComputeCategoryAnalyticsDbModel { UserId = userId, Year = year, Month = month }, ct);

        if (categoryRows.Count > 0)
            await filRepository.UpsertCategoryAnalyticsAsync(userId, categoryRows, ct);

        // 3. Build rules context.
        var recentSnapshots  = await filRepository.GetRecentSnapshotsAsync(userId, months: 3, ct);
        var currentCats      = categoryRows;
        var prevMonth        = month == 1 ? 12 : month - 1;
        var prevYear         = month == 1 ? year - 1 : year;
        var previousCats     = await filRepository.GetCategoryAnalyticsAsync(userId, prevYear, prevMonth, ct);

        var context = BuildRulesContext(userId, year, month, recentSnapshots, currentCats, previousCats);

        // 4. Evaluate rules and persist new insights.
        var insightCandidates = rulesEngine.EvaluateInsights(context);
        foreach (var candidate in insightCandidates)
        {
            var exists = await filRepository.InsightExistsForMonthAsync(userId, candidate.Code, year, month, ct);
            if (exists) continue;

            var dbModel = new CreateInsightDbModel
            {
                UserId           = userId,
                Type             = candidate.Type,
                Code             = candidate.Code,
                TitleEn          = candidate.TitleEn,
                TitleAr          = candidate.TitleAr,
                DescriptionEn    = candidate.DescriptionEn,
                DescriptionAr    = candidate.DescriptionAr,
                Severity         = candidate.Severity,
                RelatedCategoryId = candidate.RelatedCategoryId,
                DataPointJson    = candidate.DataPointJson,
                ExpiresAtUtc     = DateTime.UtcNow.AddDays(30)
            };

            await filRepository.CreateInsightAsync(dbModel, ct);

            if (candidate.FireNotification && candidate.NotificationCode is not null)
            {
                await notificationPublisher.PublishAsync(
                    candidate.NotificationCode,
                    userId,
                    parameters: candidate.NotificationParameters,
                    payload: new { code = candidate.NotificationCode },
                    ct: ct);
            }
        }

        // 5. Evaluate and persist new recommendations.
        var recoCandidates = rulesEngine.EvaluateRecommendations(context);
        foreach (var candidate in recoCandidates)
        {
            var exists = await filRepository.RecommendationExistsForMonthAsync(userId, candidate.Code, year, month, ct);
            if (exists) continue;

            await filRepository.CreateRecommendationAsync(new CreateRecommendationDbModel
            {
                UserId              = userId,
                Type                = candidate.Type,
                Code                = candidate.Code,
                TitleEn             = candidate.TitleEn,
                TitleAr             = candidate.TitleAr,
                MessageEn           = candidate.MessageEn,
                MessageAr           = candidate.MessageAr,
                ExpectedImpactValue = candidate.ExpectedImpactValue,
                Priority            = candidate.Priority,
                RelatedCategoryId   = candidate.RelatedCategoryId,
                ExpiresAtUtc        = DateTime.UtcNow.AddDays(60)
            }, ct);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Context builder
    // ═════════════════════════════════════════════════════════════════════════

    private static FinancialRulesContext BuildRulesContext(
        long                                    userId,
        int                                     year,
        int                                     month,
        IReadOnlyList<SnapshotDbResult>         recentSnapshots,
        IReadOnlyList<CategoryAnalyticsDbResult> currentCats,
        IReadOnlyList<CategoryAnalyticsDbResult> previousCats)
    {
        static SnapshotData Map(SnapshotDbResult r) => new()
        {
            Year                   = r.SnapshotDate.Year,
            Month                  = r.SnapshotDate.Month,
            TotalIncome            = r.TotalIncome,
            TotalExpense           = r.TotalExpense,
            NetBalance             = r.NetBalance,
            AverageDailySpend      = r.AverageDailySpend,
            AverageTransactionValue = r.AverageTransactionValue,
            TransactionCount       = r.TransactionCount
        };

        static CategoryPeriodData MapCat(CategoryAnalyticsDbResult r) => new()
        {
            CategoryId    = r.CategoryId,
            NameEn        = r.CategoryNameEn,
            NameAr        = r.CategoryNameAr,
            TotalSpent    = r.TotalSpent,
            TxCount       = r.TransactionCount,
            PercentOfTotal = r.PercentageOfTotal
        };

        var current  = recentSnapshots.FirstOrDefault(s => s.SnapshotDate.Year == year && s.SnapshotDate.Month == month);
        var previous = recentSnapshots.Skip(1).FirstOrDefault();

        return new FinancialRulesContext
        {
            UserId               = userId,
            Year                 = year,
            Month                = month,
            CurrentSnapshot      = current  is not null ? Map(current)  : null,
            PreviousSnapshot     = previous is not null ? Map(previous) : null,
            RecentSnapshots      = recentSnapshots.Select(Map).ToList(),
            CurrentCategoryData  = currentCats.Select(MapCat).ToList(),
            PreviousCategoryData = previousCats.Select(MapCat).ToList()
        };
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Mappers
    // ═════════════════════════════════════════════════════════════════════════

    private static SnapshotResponse MapSnapshot(SnapshotDbResult r) =>
        new(r.SnapshotDate, r.TotalIncome, r.TotalExpense, r.NetBalance,
            r.AverageDailySpend, r.AverageTransactionValue, r.TransactionCount, r.TopCategoryId);

    private static InsightResponse MapInsight(InsightRowDbResult r, bool isArabic) =>
        new(r.InsightId,
            r.Type,
            TypeName(r.Type),
            r.Code,
            isArabic ? r.TitleAr      : r.TitleEn,
            isArabic ? r.DescriptionAr : r.DescriptionEn,
            r.Severity,
            SeverityName(r.Severity),
            r.RelatedCategoryId,
            r.DataPointJson,
            r.IsRead,
            r.GeneratedAtUtc,
            r.ExpiresAtUtc);

    private static PatternResponse MapPattern(PatternDbResult r, bool isArabic) =>
        new(r.PatternId,
            r.PatternType,
            PatternTypeName(r.PatternType),
            r.Code,
            isArabic ? r.DescriptionAr : r.DescriptionEn,
            r.ConfidenceScore,
            r.DetectedAtUtc);

    private static RecommendationResponse MapRecommendation(RecommendationDbResult r, bool isArabic) =>
        new(r.RecommendationId,
            r.Type,
            RecommendationTypeName(r.Type),
            r.Code,
            isArabic ? r.TitleAr   : r.TitleEn,
            isArabic ? r.MessageAr : r.MessageEn,
            r.ExpectedImpactValue,
            r.Priority,
            r.RelatedCategoryId,
            r.IsApplied,
            r.IsDismissed,
            r.CreatedAtUtc);

    private static CategoryAnalyticsResponse MapCategoryAnalytics(CategoryAnalyticsDbResult r, bool isArabic) =>
        new(r.CategoryId,
            isArabic ? r.CategoryNameAr : r.CategoryNameEn,
            r.TotalSpent,
            r.TransactionCount,
            r.AverageSpent,
            r.PercentageOfTotal,
            r.TrendDirection,
            TrendDirectionName(r.TrendDirection),
            r.PreviousPeriodTotal,
            r.ChangePercentage);

    // ── Name helpers ──────────────────────────────────────────────────────────

    private static string TypeName(byte type) => type switch
    {
        1 => "Warning",
        2 => "Info",
        3 => "Opportunity",
        4 => "Achievement",
        _ => "Unknown"
    };

    private static string SeverityName(byte severity) => severity switch
    {
        1 => "Low",
        2 => "Medium",
        3 => "High",
        4 => "Critical",
        _ => "Unknown"
    };

    private static string PatternTypeName(byte pt) => pt switch
    {
        1 => "Daily",
        2 => "Weekly",
        3 => "Monthly",
        4 => "CategoryBased",
        _ => "Unknown"
    };

    private static string RecommendationTypeName(byte type) => type switch
    {
        1 => "ReduceSpending",
        2 => "SaveMore",
        3 => "ReviewCategory",
        4 => "SetBudget",
        5 => "IncreaseIncome",
        _ => "Unknown"
    };

    private static string TrendDirectionName(byte td) => td switch
    {
        1 => "Stable",
        2 => "Up",
        3 => "Down",
        _ => "Stable"
    };
}
