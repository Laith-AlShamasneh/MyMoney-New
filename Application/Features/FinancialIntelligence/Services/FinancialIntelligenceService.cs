using Application.Common.Constants;
using Application.Features.FinancialIntelligence.DbModels;
using Application.Features.FinancialIntelligence.DTOs;
using Application.Features.FinancialIntelligence.Rules;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Shared.Constants;
using Shared.Enums.Intelligence;
using Shared.Enums.System;
using Shared.Results;
using System.Text.Json;

namespace Application.Features.FinancialIntelligence.Services;

internal sealed class FinancialIntelligenceService(
    IFinancialIntelligenceRepository      filRepository,
    INotificationPublisher                notificationPublisher,
    IUserContext                          userContext,
    IMessageProvider                      messageProvider,
    IFinancialRulesEngine                 rulesEngine,
    ICacheService                         cacheService,
    ILogger<FinancialIntelligenceService> logger) : IFinancialIntelligenceService, IFILBackgroundProcessingService
{
    private const string DashboardCacheKeyPrefix = "fil:dashboard:";

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
            UserId      = userContext.UserId,
            WorkspaceId = userContext.WorkspaceId,
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
        var affected = await filRepository.MarkInsightReadAsync(userContext.UserId, userContext.WorkspaceId, request.InsightId, ct);

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

    public async Task<ServiceResult<object?>> MarkAllInsightsReadAsync(CancellationToken ct = default)
    {
        var userId = userContext.UserId;
        await filRepository.MarkAllInsightsReadAsync(userId, userContext.WorkspaceId, ct);
        await cacheService.RemoveAsync($"{DashboardCacheKeyPrefix}{userId}:{userContext.WorkspaceId ?? 0}");
        return ServiceResultFactory.Success<object?>(
            null,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.FinancialIntelligence.AllInsightsMarkedRead, ct));
    }

    public async Task<ServiceResult<IReadOnlyList<PatternResponse>>> GetPatternsAsync(CancellationToken ct = default)
    {
        var db       = await filRepository.GetPatternsAsync(userContext.UserId, userContext.WorkspaceId, ct);
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
            UserId      = userContext.UserId,
            WorkspaceId = userContext.WorkspaceId,
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
        var affected = await filRepository.MarkRecommendationAppliedAsync(userContext.UserId, userContext.WorkspaceId, request.RecommendationId, ct);

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
        var affected = await filRepository.DismissRecommendationAsync(userContext.UserId, userContext.WorkspaceId, request.RecommendationId, ct);

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

        var cacheKey = $"{DashboardCacheKeyPrefix}{userId}:{userContext.WorkspaceId ?? 0}";
        var cached   = await cacheService.GetAsync<FILDashboardResponse>(cacheKey);
        if (cached is not null)
        {
            var cachedMsg = await messageProvider.GetMessagesAsync(MessageKeys.FinancialIntelligence.DashboardLoaded, ct);
            return ServiceResultFactory.Success(cached, InternalResponseCodes.OK, cachedMsg);
        }

        // Load all dashboard data in parallel — each is an independent read.
        var workspaceId         = userContext.WorkspaceId;
        var snapshotTask        = filRepository.GetLatestSnapshotAsync(userId, workspaceId, ct);
        var recentSnapshotsTask = filRepository.GetRecentSnapshotsAsync(userId, workspaceId, months: 3, ct);
        var insightsTask        = filRepository.GetInsightsAsync(new GetInsightsDbModel { UserId = userId, WorkspaceId = workspaceId, IsRead = false, PageNumber = 1, PageSize = 5 }, ct);
        var patternsTask        = filRepository.GetPatternsAsync(userId, workspaceId, ct);
        var recommendTask       = filRepository.GetRecommendationsAsync(new GetRecommendationsDbModel { UserId = userId, WorkspaceId = workspaceId, PageNumber = 1, PageSize = 5 }, ct);

        await Task.WhenAll(snapshotTask, recentSnapshotsTask, insightsTask, patternsTask, recommendTask);

        var snapshot    = snapshotTask.Result;
        var latestMonth = snapshot is not null
            ? await filRepository.GetCategoryAnalyticsAsync(userId, workspaceId, snapshot.SnapshotDate.Year, snapshot.SnapshotDate.Month, ct)
            : [];

        var response = new FILDashboardResponse(
            LatestSnapshot:  snapshot is not null ? MapSnapshot(snapshot) : null,
            HealthScore:     ComputeHealthScore(snapshot, recentSnapshotsTask.Result),
            TopInsights:     insightsTask.Result.Items.Select(r => MapInsight(r, isArabic)).ToList(),
            Patterns:        patternsTask.Result.Select(p => MapPattern(p, isArabic)).ToList(),
            Recommendations: recommendTask.Result.Items.Select(r => MapRecommendation(r, isArabic)).ToList(),
            CategoryTrends:  latestMonth.Select(c => MapCategoryAnalytics(c, isArabic)).ToList()
        );

        await cacheService.SetAsync(cacheKey, response, TimeSpan.FromMinutes(60));

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
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "FIL daily processing failed for user {UserId} — {Year}-{Month:D2}",
                    user.UserId, year, month);
            }
        }

        await filRepository.CleanupExpiredInsightsAsync(ct);
        await filRepository.CleanupExpiredRecommendationsAsync(ct);
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
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "FIL monthly processing failed for user {UserId} — {Year}-{Month:D2}",
                    user.UserId, year, month);
            }
        }
    }

    public Task ProcessHourlyAnomalyAsync(DateTime fromUtc, CancellationToken ct = default) =>
        ProcessAnomalyWindowAsync(fromUtc, ct);

    public Task ProcessUserSnapshotAsync(long userId, int year, int month, CancellationToken ct = default) =>
        ProcessUserDailyAsync(userId, year, month, ct);

    private async Task ProcessAnomalyWindowAsync(DateTime fromUtc, CancellationToken ct)
    {
        var largeTx = await filRepository.GetRecentLargeTransactionsAsync(fromUtc, ct);

        foreach (var tx in largeTx)
        {
            // One UnusualTransaction insight per user per month — categoryId is not part of the key.
            var alreadyExists = await filRepository.InsightExistsForMonthAsync(
                tx.UserId, InsightCodes.UnusualTransaction,
                fromUtc.Year, fromUtc.Month, categoryId: null, ct);

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
                DataPointJson = JsonSerializer.Serialize(new
                {
                    transactionId = tx.TransactionId,
                    amount        = tx.Amount,
                    userAverage   = tx.UserAverage,
                    multiple
                }),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
            };

            var newId = await filRepository.CreateInsightAsync(dbModel, ct);

            // newId == 0 means the SP blocked a concurrent duplicate — skip notification.
            if (newId > 0)
            {
                await notificationPublisher.PublishAsync(
                    NotificationCodes.FILUnusualTransaction,
                    tx.UserId,
                    parameters: new Dictionary<string, string>
                    {
                        { "Amount",          tx.Amount.ToString("N2") },
                        { "Multiple",        multiple.ToString("F1") },
                        { "CategoryNameEn",  tx.CategoryNameEn },
                        { "CategoryNameAr",  tx.CategoryNameAr }
                    },
                    payload: new { code = NotificationCodes.FILUnusualTransaction },
                    ct: ct);

                // Anomaly compute runs in personal scope (workspace 0) for now.
                await cacheService.RemoveAsync($"{DashboardCacheKeyPrefix}{tx.UserId}:0");
            }
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

        await cacheService.RemoveAsync($"{DashboardCacheKeyPrefix}{userId}:{userContext.WorkspaceId ?? 0}");
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

        // 3. Build rules context — load 3 prior months of category data in parallel
        //    so EvaluateOverspendingAlert has a genuine rolling 3-month baseline.
        // Compute path still runs per-user (personal scope) until scheduler workspace-iteration lands.
        var recentSnapshotsTask = filRepository.GetRecentSnapshotsAsync(userId, null, months: 3, ct);

        var (pm1, py1) = PriorMonth(year,  month,  1);
        var (pm2, py2) = PriorMonth(py1,   pm1,    1);
        var (pm3, py3) = PriorMonth(py2,   pm2,    1);

        var prevCatsTask  = filRepository.GetCategoryAnalyticsAsync(userId, null, py1, pm1, ct);
        var prevCats2Task = filRepository.GetCategoryAnalyticsAsync(userId, null, py2, pm2, ct);
        var prevCats3Task = filRepository.GetCategoryAnalyticsAsync(userId, null, py3, pm3, ct);

        await Task.WhenAll(recentSnapshotsTask, prevCatsTask, prevCats2Task, prevCats3Task);

        var recentSnapshots   = recentSnapshotsTask.Result;
        var currentCats       = categoryRows;
        var previousCats      = prevCatsTask.Result;
        var rollingAverages   = BuildCategoryRollingAverages(prevCatsTask.Result, prevCats2Task.Result, prevCats3Task.Result);

        var context = BuildRulesContext(userId, year, month, recentSnapshots, currentCats, previousCats, rollingAverages);

        // 4. Evaluate rules and persist new insights.
        // Category-scoped codes (SpendingSpike, OverspendingAlert) are deduped by Code + CategoryId;
        // non-category codes are deduped by Code alone (categoryId = null).
        var insightCandidates = rulesEngine.EvaluateInsights(context);
        foreach (var candidate in insightCandidates)
        {
            var exists = await filRepository.InsightExistsForMonthAsync(
                userId, candidate.Code, year, month, candidate.RelatedCategoryId, ct);
            if (exists) continue;

            var dbModel = new CreateInsightDbModel
            {
                UserId            = userId,
                Type              = candidate.Type,
                Code              = candidate.Code,
                TitleEn           = candidate.TitleEn,
                TitleAr           = candidate.TitleAr,
                DescriptionEn     = candidate.DescriptionEn,
                DescriptionAr     = candidate.DescriptionAr,
                Severity          = candidate.Severity,
                RelatedCategoryId = candidate.RelatedCategoryId,
                DataPointJson     = candidate.DataPoint is not null ? JsonSerializer.Serialize(candidate.DataPoint) : null,
                ExpiresAtUtc      = DateTime.UtcNow.AddDays(30)
            };

            var newId = await filRepository.CreateInsightAsync(dbModel, ct);

            // newId == 0 means the SP blocked a concurrent duplicate — skip notification.
            if (newId > 0 && candidate.FireNotification && candidate.NotificationCode is not null)
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

        await cacheService.RemoveAsync($"{DashboardCacheKeyPrefix}{userId}:{userContext.WorkspaceId ?? 0}");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Context builder
    // ═════════════════════════════════════════════════════════════════════════

    private static FinancialRulesContext BuildRulesContext(
        long                                     userId,
        int                                      year,
        int                                      month,
        IReadOnlyList<SnapshotDbResult>          recentSnapshots,
        IReadOnlyList<CategoryAnalyticsDbResult> currentCats,
        IReadOnlyList<CategoryAnalyticsDbResult> previousCats,
        IReadOnlyDictionary<int, decimal>        categoryRollingAverages)
    {
        static SnapshotData Map(SnapshotDbResult r) => new()
        {
            Year                    = r.SnapshotDate.Year,
            Month                   = r.SnapshotDate.Month,
            TotalIncome             = r.TotalIncome,
            TotalExpense            = r.TotalExpense,
            NetBalance              = r.NetBalance,
            AverageDailySpend       = r.AverageDailySpend,
            AverageTransactionValue = r.AverageTransactionValue,
            TransactionCount        = r.TransactionCount
        };

        static CategoryPeriodData MapCat(CategoryAnalyticsDbResult r) => new()
        {
            CategoryId     = r.CategoryId,
            NameEn         = r.CategoryNameEn,
            NameAr         = r.CategoryNameAr,
            TotalSpent     = r.TotalSpent,
            TxCount        = r.TransactionCount,
            PercentOfTotal = r.PercentageOfTotal
        };

        var current  = recentSnapshots.FirstOrDefault(s => s.SnapshotDate.Year == year && s.SnapshotDate.Month == month);
        var previous = recentSnapshots.Skip(1).FirstOrDefault();

        return new FinancialRulesContext
        {
            UserId                  = userId,
            Year                    = year,
            Month                   = month,
            CurrentSnapshot         = current  is not null ? Map(current)  : null,
            PreviousSnapshot        = previous is not null ? Map(previous) : null,
            RecentSnapshots         = recentSnapshots.Select(Map).ToList(),
            CurrentCategoryData     = currentCats.Select(MapCat).ToList(),
            PreviousCategoryData    = previousCats.Select(MapCat).ToList(),
            CategoryRollingAverages = categoryRollingAverages
        };
    }

    // Returns the (month, year) that is n months before (year, month).
    private static (int month, int year) PriorMonth(int year, int month, int n)
    {
        var m = month - n;
        var y = year;
        while (m < 1)  { m += 12; y--; }
        return (m, y);
    }

    // Builds a CategoryId → average TotalSpent map across all provided prior-month lists.
    private static IReadOnlyDictionary<int, decimal> BuildCategoryRollingAverages(
        params IReadOnlyList<CategoryAnalyticsDbResult>[] priorMonths)
    {
        return priorMonths
            .SelectMany(m => m)
            .Where(c => c.TotalSpent > 0)
            .GroupBy(c => c.CategoryId)
            .ToDictionary(g => g.Key, g => g.Average(c => c.TotalSpent));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Financial health score
    // ═════════════════════════════════════════════════════════════════════════

    // Scores the user's financial health 0–100 from four independent factors.
    // Returns null when there is no transaction data to base a score on.
    private static FinancialHealthScore? ComputeHealthScore(
        SnapshotDbResult?               currentSnapshot,
        IReadOnlyList<SnapshotDbResult> recentSnapshots)
    {
        if (currentSnapshot is null || currentSnapshot.TransactionCount == 0)
            return null;

        var factors = new List<HealthScoreFactor>(4);
        var score   = 0;

        score += ScoreSavingsRate(currentSnapshot, factors);
        score += ScoreExpenseRatio(currentSnapshot, factors);
        score += ScoreSpendingTrend(recentSnapshots, factors);
        score += ScoreBalanceStreak(recentSnapshots, currentSnapshot, factors);

        score = Math.Clamp(score, 0, 100);

        var rating = score switch
        {
            >= 80 => "Healthy",
            >= 60 => "Good",
            >= 35 => "AtRisk",
            _     => "Poor"
        };

        return new FinancialHealthScore(score, rating, [.. factors]);
    }

    // Savings rate = NetBalance / TotalIncome. Higher savings → more points (max 30).
    private static int ScoreSavingsRate(SnapshotDbResult snap, List<HealthScoreFactor> out_)
    {
        const int Max = 30;

        if (snap.TotalIncome <= 0)
        {
            out_.Add(new HealthScoreFactor("SavingsRate", 0, Max, 0m));
            return 0;
        }

        var rate = snap.NetBalance / snap.TotalIncome;
        var pts  = rate >= 0.20m ? Max :
                   rate >= 0.10m ? 20  :
                   rate >= 0.00m ? 10  : 0;

        out_.Add(new HealthScoreFactor("SavingsRate", pts, Max, Math.Round(rate * 100, 1)));
        return pts;
    }

    // Expense ratio = TotalExpense / TotalIncome. Lower ratio → more points (max 30).
    private static int ScoreExpenseRatio(SnapshotDbResult snap, List<HealthScoreFactor> out_)
    {
        const int Max = 30;

        if (snap.TotalIncome <= 0)
        {
            var pts_ = snap.TotalExpense == 0 ? 15 : 0;
            out_.Add(new HealthScoreFactor("ExpenseRatio", pts_, Max, 0m));
            return pts_;
        }

        var ratio = snap.TotalExpense / snap.TotalIncome;
        var pts   = ratio <= 0.60m ? Max :
                    ratio <= 0.70m ? 25  :
                    ratio <= 0.80m ? 15  :
                    ratio <= 0.90m ? 8   : 0;

        out_.Add(new HealthScoreFactor("ExpenseRatio", pts, Max, Math.Round(ratio * 100, 1)));
        return pts;
    }

    // Spending trend: counts months where expenses rose >5% over the prior month.
    // Fewer rising months → more points (max 20). Neutral 10 pts when < 2 months of history.
    private static int ScoreSpendingTrend(
        IReadOnlyList<SnapshotDbResult> recentSnapshots,
        List<HealthScoreFactor>         out_)
    {
        const int Max     = 20;
        const int Neutral = 10;

        if (recentSnapshots.Count < 2)
        {
            out_.Add(new HealthScoreFactor("SpendingTrend", Neutral, Max, 0m));
            return Neutral;
        }

        var ordered = recentSnapshots
            .OrderBy(s => s.SnapshotDate)
            .ToArray();

        var risingMonths = 0;
        for (var i = 1; i < ordered.Length; i++)
        {
            if (ordered[i - 1].TotalExpense == 0) continue;
            var change = (ordered[i].TotalExpense - ordered[i - 1].TotalExpense)
                         / ordered[i - 1].TotalExpense;
            if (change > 0.05m) risingMonths++;
        }

        var pts = risingMonths == 0 ? Max :
                  risingMonths == 1 ? 12  : 5;

        out_.Add(new HealthScoreFactor("SpendingTrend", pts, Max, (decimal)risingMonths));
        return pts;
    }

    // Balance streak: counts recent months with positive NetBalance. More positive → more points (max 20).
    private static int ScoreBalanceStreak(
        IReadOnlyList<SnapshotDbResult> recentSnapshots,
        SnapshotDbResult                currentSnapshot,
        List<HealthScoreFactor>         out_)
    {
        const int Max = 20;

        if (recentSnapshots.Count == 0)
        {
            var singlePts = currentSnapshot.NetBalance > 0 ? 15 : 0;
            out_.Add(new HealthScoreFactor("BalanceStreak", singlePts, Max,
                currentSnapshot.NetBalance > 0 ? 1m : 0m));
            return singlePts;
        }

        var positiveCount = recentSnapshots.Count(s => s.NetBalance > 0);
        var total         = recentSnapshots.Count;

        var pts = (total, positiveCount) switch
        {
            (>= 3, 3) => Max,
            (>= 3, 2) => 13,
            (>= 3, 1) => 6,
            (>= 3, _) => 0,
            (2, 2)    => Max,
            (2, 1)    => 10,
            (2, _)    => 0,
            _         => recentSnapshots[0].NetBalance > 0 ? 15 : 0
        };

        out_.Add(new HealthScoreFactor("BalanceStreak", pts, Max, (decimal)positiveCount));
        return pts;
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
            isArabic ? r.TitleAr       : r.TitleEn,
            isArabic ? r.DescriptionAr : r.DescriptionEn,
            r.Severity,
            SeverityName(r.Severity),
            r.RelatedCategoryId,
            r.DataPointJson is not null ? JsonSerializer.Deserialize<JsonElement>(r.DataPointJson) : (JsonElement?)null,
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
