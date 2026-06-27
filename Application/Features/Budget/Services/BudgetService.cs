using Application.Common.Constants;
using Application.Features.Budget.DbModels;
using Application.Features.Budget.DTOs;
using Application.Features.Budget.Jobs;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Shared.Constants;
using Shared.Enums.System;
using Shared.Results;

namespace Application.Features.Budget.Services;

internal sealed class BudgetService(
    IBudgetRepository      budgetRepository,
    IUserContext            userContext,
    IMessageProvider       messageProvider,
    INotificationPublisher notificationPublisher,
    IBackgroundJobService  backgroundJobService) : IBudgetService, IBudgetComputationService
{
    // ──────────────────────────────────────────────────────────────────────────
    // API: IBudgetService  (userId always from IUserContext)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<IReadOnlyList<BudgetResponse>>> GetListAsync(
        int? statusId, CancellationToken ct = default)
    {
        var model = new GetBudgetListDbModel { UserId = userContext.UserId, WorkspaceId = userContext.WorkspaceId, StatusId = (byte?)statusId };
        var rows  = await budgetRepository.GetListAsync(model, ct);
        var items = rows.Select(MapToBudgetResponse).ToList();
        var msg   = await messageProvider.GetMessagesAsync(MessageKeys.Budget.ListLoaded, ct);
        return ServiceResultFactory.Success<IReadOnlyList<BudgetResponse>>(items, InternalResponseCodes.OK, msg);
    }

    public async Task<ServiceResult<BudgetDetailResponse>> GetByIdAsync(
        long budgetId, CancellationToken ct = default)
    {
        var (budget, history) = await budgetRepository.GetByIdAsync(userContext.UserId, userContext.WorkspaceId, budgetId, ct);
        if (budget is null)
        {
            return ServiceResultFactory.Failure<BudgetDetailResponse>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Budget.NotFound, ct));
        }

        var response = new BudgetDetailResponse(
            BudgetId:       budget.BudgetId,
            Name:           budget.Name,
            CategoryId:     budget.CategoryId,
            CategoryNameEn: budget.CategoryNameEn,
            CategoryNameAr: budget.CategoryNameAr,
            CategoryIcon:   budget.CategoryIcon,
            BudgetTypeId:   budget.BudgetTypeId,
            Amount:         budget.Amount,
            PeriodTypeId:   budget.PeriodTypeId,
            StartDate:      budget.StartDate.ToString("yyyy-MM-dd"),
            EndDate:        budget.EndDate?.ToString("yyyy-MM-dd"),
            IsAutoRenew:    budget.IsAutoRenew,
            StatusId:       budget.StatusId,
            Notes:          budget.Notes,
            CreatedAt:      budget.CreatedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            UpdatedAt:      budget.UpdatedAtUtc?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ComputedAt:     budget.ComputedAtUtc?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            CurrentPeriod:  MapToPeriodSnapshot(budget),
            History:        history.Select(MapToPeriodHistoryItem).ToList());

        var msg = await messageProvider.GetMessagesAsync(MessageKeys.Budget.GetSuccess, ct);
        return ServiceResultFactory.Success(response, InternalResponseCodes.OK, msg);
    }

    public async Task<ServiceResult<BudgetDashboardResponse>> GetDashboardAsync(CancellationToken ct = default)
    {
        var (summary, budgets, trend) = await budgetRepository.GetDashboardAsync(userContext.UserId, userContext.WorkspaceId, ct);

        var response = new BudgetDashboardResponse(
            Summary: summary is null
                ? new BudgetDashboardSummaryResponse(0, 0, 0, 0, 0, 0, 0, 0)
                : new BudgetDashboardSummaryResponse(
                    TotalBudgets:         summary.TotalBudgets,
                    ExceededCount:        summary.ExceededCount,
                    NearLimitCount:       summary.NearLimitCount,
                    OnTrackCount:         summary.OnTrackCount,
                    OverallHealthScore:   summary.OverallHealthScore,
                    TotalRemainingAmount: summary.TotalRemainingAmount,
                    TotalBudgetedAmount:  summary.TotalBudgetedAmount,
                    TotalActualSpent:     summary.TotalActualSpent),
            Budgets: budgets.Select(MapToBudgetResponse).ToList(),
            Trend:   trend.Select(t => new BudgetTrendPoint(
                PeriodStart:       t.PeriodStart.ToString("yyyy-MM-dd"),
                AvgUtilizationPct: t.AvgUtilizationPct,
                TotalBudgeted:     t.TotalBudgeted,
                TotalSpent:        t.TotalSpent,
                ExceededCount:     t.ExceededCount,
                AvgHealthScore:    t.AvgHealthScore)).ToList());

        var msg = await messageProvider.GetMessagesAsync(MessageKeys.Budget.DashboardLoaded, ct);
        return ServiceResultFactory.Success(response, InternalResponseCodes.OK, msg);
    }

    public async Task<ServiceResult<BudgetResponse>> CreateAsync(
        CreateBudgetRequest request, CancellationToken ct = default)
    {
        var userId = userContext.UserId;
        var model  = new CreateBudgetDbModel
        {
            UserId       = userId,
            WorkspaceId  = userContext.WorkspaceId,
            Name         = request.Name.Trim(),
            CategoryId   = request.CategoryId,
            BudgetTypeId = (byte)request.BudgetTypeId,
            Amount       = request.Amount,
            PeriodTypeId = (byte)request.PeriodTypeId,
            StartDate    = DateOnly.Parse(request.StartDate),
            EndDate      = request.EndDate is not null ? DateOnly.Parse(request.EndDate) : null,
            IsAutoRenew  = request.IsAutoRenew,
            Notes        = request.Notes?.Trim(),
        };

        var result = await budgetRepository.CreateAsync(model, ct);

        if (result.ResultCode == 1)
        {
            return ServiceResultFactory.Failure<BudgetResponse>(
                InternalResponseCodes.Conflict,
                await messageProvider.GetMessagesAsync(MessageKeys.Budget.DuplicateBudget, ct));
        }

        if (result.ResultCode == 2)
        {
            return ServiceResultFactory.Failure<BudgetResponse>(
                InternalResponseCodes.BadRequest,
                await messageProvider.GetMessagesAsync(MessageKeys.Budget.InvalidCategory, ct));
        }

        await backgroundJobService.EnqueueAsync(
            JobTypes.ComputeBudgetSnapshot,
            new ComputeBudgetSnapshotPayload(userId, result.NewBudgetId),
            priority: 2, maxAttempts: 3, ct: ct);

        var (budget, _) = await budgetRepository.GetByIdAsync(userId, userContext.WorkspaceId, result.NewBudgetId, ct);
        if (budget is null)
        {
            return ServiceResultFactory.Failure<BudgetResponse>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Budget.NotFound, ct));
        }

        var response = MapToBudgetResponse(budget);
        var msg      = await messageProvider.GetMessagesAsync(MessageKeys.Budget.Created, ct);
        return ServiceResultFactory.Success(response, InternalResponseCodes.Created, msg);
    }

    public async Task<ServiceResult<BudgetResponse>> UpdateAsync(
        UpdateBudgetRequest request, CancellationToken ct = default)
    {
        var userId = userContext.UserId;
        var model  = new UpdateBudgetDbModel
        {
            UserId      = userId,
            WorkspaceId = userContext.WorkspaceId,
            BudgetId    = request.Id,
            Name        = request.Name.Trim(),
            Amount      = request.Amount,
            EndDate     = request.EndDate is not null ? DateOnly.Parse(request.EndDate) : null,
            IsAutoRenew = request.IsAutoRenew,
            Notes       = request.Notes?.Trim(),
        };

        var affected = await budgetRepository.UpdateAsync(model, ct);
        if (affected == 0)
        {
            return ServiceResultFactory.Failure<BudgetResponse>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Budget.NotFound, ct));
        }

        await backgroundJobService.EnqueueAsync(
            JobTypes.ComputeBudgetSnapshot,
            new ComputeBudgetSnapshotPayload(userId, request.Id),
            priority: 2, maxAttempts: 3, ct: ct);

        var (budget, _) = await budgetRepository.GetByIdAsync(userId, userContext.WorkspaceId, request.Id, ct);
        var response    = MapToBudgetResponse(budget!);
        var msg         = await messageProvider.GetMessagesAsync(MessageKeys.Budget.Updated, ct);
        return ServiceResultFactory.Success(response, InternalResponseCodes.OK, msg);
    }

    public async Task<ServiceResult<object?>> DeleteAsync(long budgetId, CancellationToken ct = default)
    {
        var affected = await budgetRepository.DeleteAsync(
            new DeleteBudgetDbModel { UserId = userContext.UserId, WorkspaceId = userContext.WorkspaceId, BudgetId = budgetId }, ct);

        if (affected == 0)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Budget.NotFound, ct));
        }

        var msg = await messageProvider.GetMessagesAsync(MessageKeys.Budget.Deleted, ct);
        return ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK, msg);
    }

    public async Task<ServiceResult<object?>> PauseAsync(long budgetId, CancellationToken ct = default)
    {
        var (budget, _) = await budgetRepository.GetByIdAsync(userContext.UserId, userContext.WorkspaceId, budgetId, ct);
        if (budget is null)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Budget.NotFound, ct));
        }

        if (budget.StatusId == 2)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.Conflict,
                await messageProvider.GetMessagesAsync(MessageKeys.Budget.AlreadyPaused, ct));
        }

        if (budget.StatusId == 3)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.Conflict,
                await messageProvider.GetMessagesAsync(MessageKeys.Budget.CannotPauseArchived, ct));
        }

        await budgetRepository.UpdateStatusAsync(
            new UpdateBudgetStatusDbModel { UserId = userContext.UserId, WorkspaceId = userContext.WorkspaceId, BudgetId = budgetId, NewStatusId = 2 }, ct);

        var msg = await messageProvider.GetMessagesAsync(MessageKeys.Budget.Paused, ct);
        return ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK, msg);
    }

    public async Task<ServiceResult<object?>> ResumeAsync(long budgetId, CancellationToken ct = default)
    {
        var (budget, _) = await budgetRepository.GetByIdAsync(userContext.UserId, userContext.WorkspaceId, budgetId, ct);
        if (budget is null)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Budget.NotFound, ct));
        }

        if (budget.StatusId == 1)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.Conflict,
                await messageProvider.GetMessagesAsync(MessageKeys.Budget.AlreadyActive, ct));
        }

        await budgetRepository.UpdateStatusAsync(
            new UpdateBudgetStatusDbModel { UserId = userContext.UserId, WorkspaceId = userContext.WorkspaceId, BudgetId = budgetId, NewStatusId = 1 }, ct);

        var msg = await messageProvider.GetMessagesAsync(MessageKeys.Budget.Resumed, ct);
        return ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK, msg);
    }

    public async Task<ServiceResult<BudgetPeriodListResponse>> GetPeriodsAsync(
        long budgetId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var model = new GetBudgetPeriodsDbModel
        {
            UserId      = userContext.UserId,
            WorkspaceId = userContext.WorkspaceId,
            BudgetId    = budgetId,
            PageNumber = pageNumber,
            PageSize   = pageSize,
        };

        var rows       = await budgetRepository.GetPeriodsAsync(model, ct);
        var totalCount = rows.Count > 0 ? rows[0].TotalCount : 0;
        var items      = rows.Select(MapToPeriodHistoryItem).ToList();
        var response   = new BudgetPeriodListResponse(items, totalCount);
        var msg        = await messageProvider.GetMessagesAsync(MessageKeys.Budget.PeriodsLoaded, ct);
        return ServiceResultFactory.Success(response, InternalResponseCodes.OK, msg);
    }

    public async Task<ServiceResult<IReadOnlyList<BudgetAnalyticsItem>>> GetAnalyticsAsync(
        GetBudgetAnalyticsRequest request, CancellationToken ct = default)
    {
        var model = new GetBudgetAnalyticsDbModel
        {
            UserId      = userContext.UserId,
            WorkspaceId = userContext.WorkspaceId,
            BudgetId    = request.BudgetId,
            DateFrom = request.DateFrom is not null ? DateOnly.Parse(request.DateFrom) : null,
            DateTo   = request.DateTo   is not null ? DateOnly.Parse(request.DateTo)   : null,
        };

        var rows  = await budgetRepository.GetAnalyticsAsync(model, ct);
        var items = rows.Select(r => new BudgetAnalyticsItem(
            BudgetId:         r.BudgetId,
            BudgetName:       r.BudgetName,
            CategoryId:       r.CategoryId,
            CategoryNameEn:   r.CategoryNameEn,
            CategoryNameAr:   r.CategoryNameAr,
            PeriodTypeId:     r.PeriodTypeId,
            PeriodId:         r.PeriodId,
            PeriodStart:      r.PeriodStart.ToString("yyyy-MM-dd"),
            PeriodEnd:        r.PeriodEnd.ToString("yyyy-MM-dd"),
            BudgetedAmount:   r.BudgetedAmount,
            ActualSpent:      r.ActualSpent,
            UtilizationPct:   r.UtilizationPct,
            OverBudgetAmount: r.OverBudgetAmount,
            HealthScore:      r.HealthScore,
            HealthBandId:     r.HealthBandId,
            ForecastRiskId:   r.ForecastRiskId,
            PeriodStatusId:   r.PeriodStatusId,
            ClosedAt:         r.ClosedAtUtc?.ToString("yyyy-MM-ddTHH:mm:ssZ")
        )).ToList();

        var msg = await messageProvider.GetMessagesAsync(MessageKeys.Budget.AnalyticsLoaded, ct);
        return ServiceResultFactory.Success<IReadOnlyList<BudgetAnalyticsItem>>(items, InternalResponseCodes.OK, msg);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Background: IBudgetComputationService  (userId passed explicitly)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task ComputeUserBudgetSnapshotAsync(
        long userId, long budgetId, CancellationToken ct = default)
    {
        var result = await budgetRepository.ComputeSnapshotAsync(userId, budgetId, ct);
        if (result.PeriodId == 0) return;

        if (result.Alert80PctTriggered)
        {
            // Job path: the budget's workspace is resolved DB-side by primary key.
            var (budget, _) = await budgetRepository.GetByIdAsync(userId, null, budgetId, ct);
            if (budget is not null)
            {
                await notificationPublisher.PublishAsync(
                    NotificationCodes.BudgetNearingLimit,
                    userId,
                    parameters: new Dictionary<string, string>
                    {
                        ["budgetName"]  = budget.Name,
                        ["utilization"] = ((int)budget.UtilizationPct).ToString(),
                        ["remaining"]   = budget.RemainingAmount.ToString("F2"),
                        ["currency"]    = "JOD",
                    },
                    ct: ct);
            }
        }

        if (result.Alert100PctTriggered)
        {
            // Job path: the budget's workspace is resolved DB-side by primary key.
            var (budget, _) = await budgetRepository.GetByIdAsync(userId, null, budgetId, ct);
            if (budget is not null)
            {
                await notificationPublisher.PublishAsync(
                    NotificationCodes.BudgetExceeded,
                    userId,
                    parameters: new Dictionary<string, string>
                    {
                        ["budgetName"] = budget.Name,
                        ["overAmount"] = budget.OverBudgetAmount.ToString("F2"),
                        ["currency"]   = "JOD",
                    },
                    ct: ct);
            }
        }
    }

    public async Task RunDailyMaintenanceAsync(long? userId, CancellationToken ct = default)
    {
        var (_, createdCount) = await budgetRepository.CloseExpiredPeriodsAsync(userId, ct);

        if (userId.HasValue)
        {
            var activeBudgets = await budgetRepository.GetActiveBudgetsForUserAsync(userId.Value, ct);
            foreach (var b in activeBudgets)
            {
                await budgetRepository.ComputeSnapshotAsync(userId.Value, b.BudgetId, ct);
            }

            if (createdCount > 0)
            {
                var allBudgets = await budgetRepository.GetListAsync(
                    new GetBudgetListDbModel { UserId = userId.Value, StatusId = 1 }, ct);

                foreach (var b in allBudgets.Where(b => b.PeriodId.HasValue))
                {
                    await notificationPublisher.PublishAsync(
                        NotificationCodes.BudgetPeriodReset,
                        userId.Value,
                        parameters: new Dictionary<string, string>
                        {
                            ["budgetName"]   = b.Name,
                            ["budgetAmount"] = b.BudgetedAmount.ToString("F2"),
                            ["currency"]     = "JOD",
                        },
                        ct: ct);
                }
            }
        }
        else
        {
            var users = await budgetRepository.GetActiveUserIdsAsync(ct);
            foreach (var u in users)
            {
                await backgroundJobService.EnqueueAsync(
                    JobTypes.BudgetDailyMaintenance,
                    new BudgetDailyMaintenancePayload(u.UserId),
                    priority: 3, maxAttempts: 2, ct: ct);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Private mapping helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static BudgetPeriodSnapshot? MapToPeriodSnapshot(BudgetRowDbResult r)
    {
        if (!r.PeriodId.HasValue) return null;
        return new BudgetPeriodSnapshot(
            PeriodId:             r.PeriodId.Value,
            PeriodStart:          r.PeriodStart!.Value.ToString("yyyy-MM-dd"),
            PeriodEnd:            r.PeriodEnd!.Value.ToString("yyyy-MM-dd"),
            BudgetedAmount:       r.BudgetedAmount,
            ActualSpent:          r.ActualSpent,
            UtilizationPct:       r.UtilizationPct,
            RemainingAmount:      r.RemainingAmount,
            OverBudgetAmount:     r.OverBudgetAmount,
            ProjectedEndSpending: r.ProjectedEndSpending,
            DailyBudgetRemaining: r.DailyBudgetRemaining,
            ForecastRiskId:       r.ForecastRiskId,
            HealthScore:          r.HealthScore,
            HealthBandId:         r.HealthBandId,
            PeriodStatusId:       r.PeriodStatusId);
    }

    private static BudgetResponse MapToBudgetResponse(BudgetRowDbResult r) => new(
        BudgetId:       r.BudgetId,
        Name:           r.Name,
        CategoryId:     r.CategoryId,
        CategoryNameEn: r.CategoryNameEn,
        CategoryNameAr: r.CategoryNameAr,
        CategoryIcon:   r.CategoryIcon,
        BudgetTypeId:   r.BudgetTypeId,
        Amount:         r.Amount,
        PeriodTypeId:   r.PeriodTypeId,
        StartDate:      r.StartDate.ToString("yyyy-MM-dd"),
        EndDate:        r.EndDate?.ToString("yyyy-MM-dd"),
        IsAutoRenew:    r.IsAutoRenew,
        StatusId:       r.StatusId,
        Notes:          r.Notes,
        CreatedAt:      r.CreatedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        CurrentPeriod:  MapToPeriodSnapshot(r));

    private static BudgetPeriodHistoryItem MapToPeriodHistoryItem(BudgetPeriodRowDbResult r) => new(
        PeriodId:         r.PeriodId,
        PeriodStart:      r.PeriodStart.ToString("yyyy-MM-dd"),
        PeriodEnd:        r.PeriodEnd.ToString("yyyy-MM-dd"),
        BudgetedAmount:   r.BudgetedAmount,
        ActualSpent:      r.ActualSpent,
        UtilizationPct:   r.UtilizationPct,
        OverBudgetAmount: r.OverBudgetAmount,
        HealthScore:      r.HealthScore,
        HealthBandId:     r.HealthBandId,
        ClosedAt:         r.ClosedAtUtc?.ToString("yyyy-MM-ddTHH:mm:ssZ"));
}
