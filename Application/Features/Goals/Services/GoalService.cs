using Application.Common.Constants;
using Application.Features.CashFlow.Jobs;
using Application.Features.Goals.DbModels;
using Application.Features.Goals.DTOs;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Shared.Constants;
using Shared.Enums.Goals;
using Shared.Enums.System;
using Shared.Results;

namespace Application.Features.Goals.Services;

internal sealed class GoalService(
    IGoalRepository        goalRepository,
    IUserContext            userContext,
    IMessageProvider        messageProvider,
    INotificationPublisher  notificationPublisher,
    IBackgroundJobService   backgroundJobService,
    ICacheService           cacheService) : IGoalService
{
    // ── List ──────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<GoalListResponse>> GetListAsync(
        GetGoalListRequest request, CancellationToken ct = default)
    {
        var model = new GetGoalsDbModel
        {
            UserId     = userContext.UserId,
            StatusId   = request.StatusId.HasValue   ? (byte?)request.StatusId.Value   : null,
            GoalTypeId = request.GoalTypeId.HasValue ? (byte?)request.GoalTypeId.Value : null,
            Priority   = request.Priority.HasValue   ? (byte?)request.Priority.Value   : null,
            PageNumber = request.PageNumber,
            PageSize   = request.PageSize,
        };

        var db = await goalRepository.GetListAsync(model, ct);

        var items = db.Items.Select(g => new GoalListItemDto(
            GoalId:               g.GoalId,
            Name:                 g.Name,
            Description:          g.Description,
            GoalTypeId:           g.GoalTypeId,
            TargetAmount:         g.TargetAmount,
            CurrentAmount:        g.CurrentAmount,
            TargetDate:           g.TargetDate?.ToString("yyyy-MM-dd"),
            Priority:             g.Priority,
            StatusId:             g.StatusId,
            Icon:                 g.Icon,
            Color:                g.Color,
            CreatedAt:            g.CreatedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            CompletedAt:          g.CompletedAtUtc?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            CompletionPercent:    g.CompletionPercent,
            LastContributionDate: g.LastContributionDate?.ToString("yyyy-MM-dd"),
            LinkedRecurringCount: g.LinkedRecurringCount
        )).ToList();

        var response = new GoalListResponse(
            Items:       items,
            TotalCount:  db.TotalCount,
            PageNumber:  request.PageNumber,
            PageSize:    request.PageSize);

        var message = await messageProvider.GetMessagesAsync(MessageKeys.Goal.ListLoaded, ct);
        return ServiceResultFactory.Success(response, InternalResponseCodes.OK, message);
    }

    // ── Get by ID ─────────────────────────────────────────────────────────────

    public async Task<ServiceResult<GoalDetailResponse>> GetByIdAsync(
        long goalId, CancellationToken ct = default)
    {
        var goal = await goalRepository.GetByIdAsync(goalId, userContext.UserId, ct);
        if (goal is null)
        {
            return ServiceResultFactory.Failure<GoalDetailResponse>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Goal.NotFound, ct));
        }

        // Parallel load of auxiliary data
        var statsTask      = goalRepository.GetMonthlyStatsAsync(goalId, userContext.UserId, 3, ct);
        var milestonesTask = goalRepository.GetMilestonesAsync(goalId, userContext.UserId, ct);
        var linksTask      = goalRepository.GetRecurringLinksAsync(goalId, userContext.UserId, ct);
        await Task.WhenAll(statsTask, milestonesTask, linksTask);

        var progress   = ComputeProgress(goal.TargetAmount, goal.CurrentAmount, goal.TargetDate, statsTask.Result);
        var milestones = milestonesTask.Result.Select(m => new MilestoneDto(
            MilestonePercent: m.MilestonePercent,
            ReachedAt:        m.ReachedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            IsNotified:       m.NotifiedAtUtc.HasValue)).ToList();

        var links = linksTask.Result.Select(l => new RecurringLinkDto(
            LinkId:                l.LinkId,
            RecurringDefinitionId: l.RecurringDefinitionId,
            RecurringName:         l.RecurringName,
            RecurringAmount:       l.RecurringAmount,
            FrequencyId:           l.FrequencyId,
            RecurringStatusId:     l.RecurringStatusId,
            LinkedAt:              l.CreatedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"))).ToList();

        var response = new GoalDetailResponse(
            GoalId:               goal.GoalId,
            Name:                 goal.Name,
            Description:          goal.Description,
            GoalTypeId:           goal.GoalTypeId,
            TargetAmount:         goal.TargetAmount,
            CurrentAmount:        goal.CurrentAmount,
            TargetDate:           goal.TargetDate?.ToString("yyyy-MM-dd"),
            Priority:             goal.Priority,
            StatusId:             goal.StatusId,
            Icon:                 goal.Icon,
            Color:                goal.Color,
            CreatedAt:            goal.CreatedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            CompletedAt:          goal.CompletedAtUtc?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ContributionCount:    goal.ContributionCount,
            LinkedRecurringCount: goal.LinkedRecurringCount,
            Progress:             progress,
            Milestones:           milestones,
            RecurringLinks:       links);

        var message = await messageProvider.GetMessagesAsync(MessageKeys.Goal.GetSuccess, ct);
        return ServiceResultFactory.Success(response, InternalResponseCodes.OK, message);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<CreateGoalResponse>> CreateAsync(
        CreateGoalRequest request, CancellationToken ct = default)
    {
        var model = new CreateGoalDbModel
        {
            UserId        = userContext.UserId,
            Name          = request.Name.Trim(),
            Description   = request.Description?.Trim(),
            GoalTypeId    = (byte)request.GoalTypeId,
            TargetAmount  = request.TargetAmount,
            InitialAmount = request.InitialAmount ?? 0,
            TargetDate    = TryParseDate(request.TargetDate),
            Priority      = (byte)(request.Priority ?? (int)GoalPriority.Medium),
            Icon          = request.Icon?.Trim(),
            Color         = request.Color?.Trim(),
        };

        var newId = await goalRepository.CreateAsync(model, ct);
        var message = await messageProvider.GetMessagesAsync(MessageKeys.Goal.Created, ct);
        return ServiceResultFactory.Success(
            new CreateGoalResponse(newId),
            InternalResponseCodes.Created,
            message);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<object?>> UpdateAsync(
        UpdateGoalRequest request, CancellationToken ct = default)
    {
        var model = new UpdateGoalDbModel
        {
            GoalId       = request.Id,
            UserId       = userContext.UserId,
            Name         = request.Name.Trim(),
            Description  = request.Description?.Trim(),
            TargetAmount = request.TargetAmount,
            TargetDate   = TryParseDate(request.TargetDate),
            Priority     = (byte)request.Priority,
            Icon         = request.Icon?.Trim(),
            Color        = request.Color?.Trim(),
        };

        var affected = await goalRepository.UpdateAsync(model, ct);
        if (affected == 0)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Goal.NotFound, ct));
        }

        var message = await messageProvider.GetMessagesAsync(MessageKeys.Goal.Updated, ct);
        return ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK, message);
    }

    // ── Delete (archive) ──────────────────────────────────────────────────────

    public async Task<ServiceResult<object?>> DeleteAsync(
        long goalId, CancellationToken ct = default)
    {
        var affected = await goalRepository.SetStatusAsync(
            goalId, userContext.UserId, (byte)GoalStatus.Archived, ct);

        if (affected == 0)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Goal.NotFound, ct));
        }

        var message = await messageProvider.GetMessagesAsync(MessageKeys.Goal.Deleted, ct);
        return ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK, message);
    }

    // ── Pause / Resume ────────────────────────────────────────────────────────

    public async Task<ServiceResult<object?>> PauseAsync(long goalId, CancellationToken ct = default)
    {
        var goal = await goalRepository.GetByIdAsync(goalId, userContext.UserId, ct);
        if (goal is null)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Goal.NotFound, ct));
        }

        if (goal.StatusId == (byte)GoalStatus.Paused)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.Conflict,
                await messageProvider.GetMessagesAsync(MessageKeys.Goal.AlreadyPaused, ct));
        }

        await goalRepository.SetStatusAsync(goalId, userContext.UserId, (byte)GoalStatus.Paused, ct);
        var message = await messageProvider.GetMessagesAsync(MessageKeys.Goal.Paused, ct);
        return ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK, message);
    }

    public async Task<ServiceResult<object?>> ResumeAsync(long goalId, CancellationToken ct = default)
    {
        var goal = await goalRepository.GetByIdAsync(goalId, userContext.UserId, ct);
        if (goal is null)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Goal.NotFound, ct));
        }

        if (goal.StatusId == (byte)GoalStatus.Active)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.Conflict,
                await messageProvider.GetMessagesAsync(MessageKeys.Goal.AlreadyActive, ct));
        }

        await goalRepository.SetStatusAsync(goalId, userContext.UserId, (byte)GoalStatus.Active, ct);
        var message = await messageProvider.GetMessagesAsync(MessageKeys.Goal.Resumed, ct);
        return ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK, message);
    }

    // ── Contributions ─────────────────────────────────────────────────────────

    public async Task<ServiceResult<AddContributionResponse>> AddContributionAsync(
        AddContributionRequest request, CancellationToken ct = default)
    {
        var model = new AddContributionDbModel
        {
            GoalId             = request.GoalId,
            UserId             = userContext.UserId,
            ContributionTypeId = (byte)ContributionType.Contribution,
            Amount             = request.Amount,
            IsDebit            = false,
            Notes              = request.Notes?.Trim(),
            ContributionDate   = DateOnly.Parse(request.ContributionDate),
        };

        return await InternalAddAsync(
            model,
            MessageKeys.Goal.ContributionAdded,
            ct);
    }

    public async Task<ServiceResult<AddContributionResponse>> WithdrawAsync(
        WithdrawRequest request, CancellationToken ct = default)
    {
        var model = new AddContributionDbModel
        {
            GoalId             = request.GoalId,
            UserId             = userContext.UserId,
            ContributionTypeId = (byte)ContributionType.Withdrawal,
            Amount             = request.Amount,
            IsDebit            = true,
            Notes              = request.Notes?.Trim(),
            ContributionDate   = DateOnly.Parse(request.ContributionDate),
        };

        return await InternalAddAsync(
            model,
            MessageKeys.Goal.WithdrawalAdded,
            ct);
    }

    public async Task<ServiceResult<AddContributionResponse>> AdjustAsync(
        AdjustGoalRequest request, CancellationToken ct = default)
    {
        var goal = await goalRepository.GetByIdAsync(request.GoalId, userContext.UserId, ct);
        if (goal is null)
        {
            return ServiceResultFactory.Failure<AddContributionResponse>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Goal.NotFound, ct));
        }

        var delta = request.NewAmount - goal.CurrentAmount;

        if (delta == 0)
        {
            var noOpMessage = await messageProvider.GetMessagesAsync(MessageKeys.Goal.NoAdjustmentNeeded, ct);
            return ServiceResultFactory.Success(
                new AddContributionResponse(0, goal.CurrentAmount,
                    goal.TargetAmount > 0 ? Math.Round(goal.CurrentAmount / goal.TargetAmount * 100, 2) : 0, false),
                InternalResponseCodes.OK,
                noOpMessage);
        }

        var model = new AddContributionDbModel
        {
            GoalId             = request.GoalId,
            UserId             = userContext.UserId,
            ContributionTypeId = (byte)ContributionType.Adjustment,
            Amount             = Math.Abs(delta),
            IsDebit            = delta < 0,
            Notes              = request.Notes?.Trim(),
            ContributionDate   = DateOnly.Parse(request.AdjustmentDate),
        };

        return await InternalAddAsync(
            model,
            MessageKeys.Goal.AdjustmentApplied,
            ct);
    }

    private async Task<ServiceResult<AddContributionResponse>> InternalAddAsync(
        AddContributionDbModel model,
        string successMessageKey,
        CancellationToken ct)
    {
        var result = await goalRepository.AddContributionAsync(model, ct);

        if (result.ErrorCode == -1)
        {
            return ServiceResultFactory.Failure<AddContributionResponse>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Goal.NotFound, ct));
        }

        if (result.ErrorCode == -2)
        {
            return ServiceResultFactory.Failure<AddContributionResponse>(
                InternalResponseCodes.Conflict,
                await messageProvider.GetMessagesAsync(MessageKeys.Goal.CannotContributeStatus, ct));
        }

        if (result.ErrorCode == -3)
        {
            return ServiceResultFactory.Failure<AddContributionResponse>(
                InternalResponseCodes.BadRequest,
                await messageProvider.GetMessagesAsync(MessageKeys.Goal.InsufficientBalance, ct));
        }

        // Fire milestone notifications for newly reached milestones
        foreach (var pct in result.NewMilestones)
        {
            if (pct == 100) continue; // handled below via GoalCompleted flag

            await notificationPublisher.PublishAsync(
                NotificationCodes.GoalMilestoneReached,
                userContext.UserId,
                parameters: new Dictionary<string, string>
                {
                    ["GoalName"]         = result.GoalName,
                    ["MilestonePercent"] = pct.ToString(),
                    ["SavedAmount"]      = result.NewCurrentAmount.ToString("N2"),
                },
                ct: ct);

            await goalRepository.MarkMilestoneNotifiedAsync(model.GoalId, pct, ct);
        }

        if (result.GoalCompleted)
        {
            await notificationPublisher.PublishAsync(
                NotificationCodes.GoalCompleted,
                userContext.UserId,
                parameters: new Dictionary<string, string>
                {
                    ["GoalName"]   = result.GoalName,
                    ["SavedAmount"] = result.NewCurrentAmount.ToString("N2"),
                },
                ct: ct);

            await goalRepository.MarkMilestoneNotifiedAsync(model.GoalId, 100, ct);
        }

        await cacheService.RemoveAsync($"cashflow:forecast:{model.UserId}:12");
        await cacheService.RemoveAsync($"cashflow:dashboard:{model.UserId}");
        await backgroundJobService.EnqueueAsync(
            JobTypes.ComputeCashFlowForecast,
            new ComputeForecastPayload(model.UserId),
            priority: 5,
            ct: ct);

        var message = await messageProvider.GetMessagesAsync(successMessageKey, ct);
        return ServiceResultFactory.Success(
            new AddContributionResponse(
                result.ContributionId,
                result.NewCurrentAmount,
                result.NewCompletionPercent,
                result.GoalCompleted),
            InternalResponseCodes.OK,
            message);
    }

    // ── Contribution list ─────────────────────────────────────────────────────

    public async Task<ServiceResult<ContributionListResponse>> GetContributionsAsync(
        GetContributionsRequest request, CancellationToken ct = default)
    {
        var model = new GetContributionsDbModel
        {
            UserId     = userContext.UserId,
            GoalId     = request.GoalId,
            PageNumber = request.PageNumber,
            PageSize   = request.PageSize,
        };

        var db = await goalRepository.GetContributionsAsync(model, ct);

        var items = db.Items.Select(c => new ContributionDto(
            ContributionId:      c.ContributionId,
            ContributionTypeId:  c.ContributionTypeId,
            Amount:              c.Amount,
            IsDebit:             c.IsDebit,
            Notes:               c.Notes,
            ContributionDate:    c.ContributionDate.ToString("yyyy-MM-dd"),
            CreatedAt:           c.CreatedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            SourceRecurringId:   c.SourceRecurringId,
            LinkedTransactionId: c.LinkedTransactionId
        )).ToList();

        var response = new ContributionListResponse(
            Items:      items,
            TotalCount: db.TotalCount,
            PageNumber: request.PageNumber,
            PageSize:   request.PageSize);

        var message = await messageProvider.GetMessagesAsync(MessageKeys.Goal.ContributionListLoaded, ct);
        return ServiceResultFactory.Success(response, InternalResponseCodes.OK, message);
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    public async Task<ServiceResult<GoalDashboardResponse>> GetDashboardAsync(CancellationToken ct = default)
    {
        var db = await goalRepository.GetDashboardAsync(userContext.UserId, ct);

        var kpi = new GoalDashboardKpi(
            ActiveGoalCount:     db.Kpi.ActiveGoalCount,
            PausedGoalCount:     db.Kpi.PausedGoalCount,
            CompletedGoalCount:  db.Kpi.CompletedGoalCount,
            TotalTargetAmount:   db.Kpi.TotalTargetAmount,
            TotalSavedAmount:    db.Kpi.TotalSavedAmount,
            TotalRemainingAmount: db.Kpi.TotalRemainingAmount);

        var topGoals = db.TopGoals.Select(g => new GoalDashboardItemDto(
            GoalId:            g.GoalId,
            Name:              g.Name,
            GoalTypeId:        g.GoalTypeId,
            TargetAmount:      g.TargetAmount,
            CurrentAmount:     g.CurrentAmount,
            TargetDate:        g.TargetDate?.ToString("yyyy-MM-dd"),
            Priority:          g.Priority,
            Icon:              g.Icon,
            Color:             g.Color,
            CompletionPercent: g.CompletionPercent
        )).ToList();

        var message = await messageProvider.GetMessagesAsync(MessageKeys.Goal.DashboardLoaded, ct);
        return ServiceResultFactory.Success(
            new GoalDashboardResponse(kpi, topGoals),
            InternalResponseCodes.OK,
            message);
    }

    // ── Recurring links ───────────────────────────────────────────────────────

    public async Task<ServiceResult<object?>> LinkRecurringAsync(
        LinkRecurringRequest request, CancellationToken ct = default)
    {
        var model = new GoalRecurringLinkDbModel
        {
            UserId                = userContext.UserId,
            GoalId                = request.GoalId,
            RecurringDefinitionId = request.RecurringDefinitionId,
        };

        var success = await goalRepository.UpsertRecurringLinkAsync(model, ct);
        if (!success)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Goal.RecurringLinkFailed, ct));
        }

        var message = await messageProvider.GetMessagesAsync(MessageKeys.Goal.RecurringLinked, ct);
        return ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK, message);
    }

    public async Task<ServiceResult<object?>> UnlinkRecurringAsync(
        UnlinkRecurringRequest request, CancellationToken ct = default)
    {
        var affected = await goalRepository.DeleteRecurringLinkAsync(
            request.GoalId, userContext.UserId, request.RecurringDefinitionId, ct);

        if (affected == 0)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Goal.NotFound, ct));
        }

        var message = await messageProvider.GetMessagesAsync(MessageKeys.Goal.RecurringUnlinked, ct);
        return ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK, message);
    }

    // ── Background job helpers ────────────────────────────────────────────────

    public async Task SyncAutoContributionsAsync(DateOnly date, CancellationToken ct = default)
    {
        var pending = await goalRepository.GetPendingAutoContributionsAsync(date, ct);

        foreach (var item in pending)
        {
            var model = new AddContributionDbModel
            {
                GoalId              = item.GoalId,
                UserId              = item.UserId,
                ContributionTypeId  = (byte)ContributionType.AutomaticContribution,
                Amount              = item.Amount,
                IsDebit             = false,
                ContributionDate    = item.TransactionDate,
                SourceRecurringId   = null,
                LinkedTransactionId = item.TransactionId,
            };

            var result = await goalRepository.AddContributionAsync(model, ct);
            if (result.ErrorCode != 0) continue;

            foreach (var pct in result.NewMilestones)
            {
                if (pct == 100) continue;
                await notificationPublisher.PublishAsync(
                    NotificationCodes.GoalMilestoneReached,
                    item.UserId,
                    parameters: new Dictionary<string, string>
                    {
                        ["GoalName"]         = result.GoalName,
                        ["MilestonePercent"] = pct.ToString(),
                        ["SavedAmount"]      = result.NewCurrentAmount.ToString("N2"),
                    },
                    ct: ct);
                await goalRepository.MarkMilestoneNotifiedAsync(item.GoalId, pct, ct);
            }

            if (result.GoalCompleted)
            {
                await notificationPublisher.PublishAsync(
                    NotificationCodes.GoalCompleted,
                    item.UserId,
                    parameters: new Dictionary<string, string>
                    {
                        ["GoalName"]   = result.GoalName,
                        ["SavedAmount"] = result.NewCurrentAmount.ToString("N2"),
                    },
                    ct: ct);
                await goalRepository.MarkMilestoneNotifiedAsync(item.GoalId, 100, ct);
            }
        }
    }

    public async Task CheckBehindScheduleAsync(CancellationToken ct = default)
    {
        var goals = await goalRepository.GetActiveGoalsForScheduleCheckAsync(ct);

        foreach (var goal in goals)
        {
            // Linear-rate check: if current completion < expected completion at linear pace
            if (goal.TotalDays <= 0) continue;

            var expectedPercent = (decimal)goal.DaysElapsed / goal.TotalDays * 100;
            if (goal.CompletionPercent >= expectedPercent) continue;

            var daysRemaining   = goal.TotalDays - goal.DaysElapsed;
            var remainingAmount = goal.TargetAmount - goal.CurrentAmount;
            var monthsRemaining = daysRemaining / 30.44m;
            var monthlyNeeded   = monthsRemaining > 0
                ? Math.Round(remainingAmount / monthsRemaining, 2)
                : remainingAmount;

            await notificationPublisher.PublishAsync(
                NotificationCodes.GoalBehindSchedule,
                goal.UserId,
                parameters: new Dictionary<string, string>
                {
                    ["GoalName"]             = goal.Name,
                    ["MonthlySavingsNeeded"] = monthlyNeeded.ToString("N2"),
                    ["TargetDate"]           = goal.TargetDate.ToString("yyyy-MM-dd"),
                },
                ct: ct);

            await goalRepository.UpdateBehindScheduleNotifiedAsync(goal.GoalId, ct);
        }
    }

    // ── Progress Engine ───────────────────────────────────────────────────────

    private static GoalProgress ComputeProgress(
        decimal targetAmount,
        decimal currentAmount,
        DateOnly? targetDate,
        IReadOnlyList<MonthlyStatsDbResult> monthlyStats)
    {
        var today              = DateOnly.FromDateTime(DateTime.UtcNow);
        var completionPercent  = targetAmount > 0
            ? Math.Min(Math.Round(currentAmount / targetAmount * 100, 2), 100m)
            : 0m;
        var remainingAmount    = Math.Max(targetAmount - currentAmount, 0m);

        // Average monthly net contribution (positive months only, last 3 months)
        decimal? avgMonthly = null;
        if (monthlyStats.Count > 0)
        {
            var netList = monthlyStats
                .Select(s => s.TotalContributed - s.TotalWithdrawn)
                .Where(n => n > 0)
                .ToList();

            if (netList.Count > 0)
                avgMonthly = Math.Round(netList.Average(), 2);
        }

        // Estimated completion date
        string? estimatedCompletion = null;
        if (remainingAmount == 0)
        {
            estimatedCompletion = today.ToString("yyyy-MM-dd");
        }
        else if (avgMonthly is > 0)
        {
            var monthsNeeded   = (double)(remainingAmount / avgMonthly.Value);
            var daysNeeded     = (int)Math.Ceiling(monthsNeeded * 30.44);
            estimatedCompletion = today.AddDays(daysNeeded).ToString("yyyy-MM-dd");
        }

        // On-track
        bool? onTrack = null;
        if (targetDate.HasValue && estimatedCompletion is not null &&
            DateOnly.TryParse(estimatedCompletion, out var estDate))
        {
            onTrack = estDate <= targetDate.Value;
        }

        // Monthly savings needed to hit deadline
        decimal? monthlySavingsNeeded = null;
        if (targetDate.HasValue && remainingAmount > 0)
        {
            var daysToTarget = targetDate.Value.DayNumber - today.DayNumber;
            if (daysToTarget > 0)
            {
                var monthsToTarget = daysToTarget / 30.44m;
                if (monthsToTarget > 0)
                    monthlySavingsNeeded = Math.Round(remainingAmount / monthsToTarget, 2);
            }
        }

        return new GoalProgress(
            CompletionPercent:       completionPercent,
            SavedAmount:             currentAmount,
            RemainingAmount:         remainingAmount,
            AvgMonthlyContribution:  avgMonthly,
            EstimatedCompletionDate: estimatedCompletion,
            OnTrack:                 onTrack,
            MonthlySavingsNeeded:    monthlySavingsNeeded);
    }

    private static DateOnly? TryParseDate(string? s) =>
        !string.IsNullOrEmpty(s) && DateOnly.TryParse(s, out var d) ? d : null;
}
