using Application.Common.Constants;
using Application.Features.CashFlow.Jobs;
using Application.Features.RecurringTransactions.DbModels;
using Application.Features.RecurringTransactions.DTOs;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Shared.Constants;
using Shared.Enums.RecurringTransactions;
using Shared.Enums.System;
using Shared.Responses;
using Shared.Results;

namespace Application.Features.RecurringTransactions.Services;

internal sealed class RecurringTransactionService(
    IRecurringTransactionRepository repository,
    IUserContext                     userContext,
    IMessageProvider                 messageProvider,
    INotificationPublisher           notificationPublisher,
    IBackgroundJobService            backgroundJobService,
    ICacheService                    cacheService) : IRecurringTransactionService, IRecurringTransactionEngineService
{
    // ── CRUD ───────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<RecurringTransactionResponse>> CreateAsync(
        CreateRecurringTransactionRequest request,
        CancellationToken                 ct = default)
    {
        var startDate  = DateOnly.Parse(request.StartDate);
        var endDate    = request.EndDate is not null ? DateOnly.Parse(request.EndDate) : (DateOnly?)null;
        var frequency  = (RecurrenceFrequency)request.FrequencyId;

        if (endDate.HasValue && endDate.Value <= startDate)
        {
            return ServiceResultFactory.Failure<RecurringTransactionResponse>(
                InternalResponseCodes.BadRequest,
                await messageProvider.GetMessagesAsync(MessageKeys.RecurringTransaction.EndDateBeforeStartDate, ct));
        }

        var nextDate = RecurringTransactionDateCalculator.ComputeFirstGenerationDate(
            startDate, frequency,
            request.DayOfMonth.HasValue ? (byte?)request.DayOfMonth.Value : null,
            request.DayOfWeek.HasValue  ? (byte?)request.DayOfWeek.Value  : null);

        var model = new CreateRecurringTransactionDbModel
        {
            UserId            = userContext.UserId,
            CategoryId        = request.CategoryId,
            TransactionTypeId = (byte)request.TransactionTypeId,
            Name              = request.Name.Trim(),
            Amount            = request.Amount,
            Description       = request.Description?.Trim(),
            FrequencyId       = (byte)request.FrequencyId,
            FrequencyInterval = request.FrequencyInterval,
            FrequencyUnit     = request.FrequencyUnit.HasValue ? (byte?)request.FrequencyUnit.Value : null,
            DayOfMonth        = request.DayOfMonth.HasValue ? (byte?)request.DayOfMonth.Value : null,
            DayOfWeek         = request.DayOfWeek.HasValue  ? (byte?)request.DayOfWeek.Value  : null,
            StartDate         = startDate,
            EndDate           = endDate,
            IsSubscription    = false,
            Notes             = request.Notes?.Trim(),
            NextGenerationDate = nextDate,
        };

        var id = await repository.CreateAsync(model, ct);

        var created = await repository.GetByIdAsync(id, ct);

        await InvalidateForecastCacheAndEnqueueAsync(userContext.UserId, ct);

        return ServiceResultFactory.Success(
            MapToResponse(created!),
            InternalResponseCodes.Created,
            await messageProvider.GetMessagesAsync(MessageKeys.RecurringTransaction.Created, ct));
    }

    public async Task<ServiceResult<RecurringTransactionResponse>> GetByIdAsync(
        long              id,
        CancellationToken ct = default)
    {
        var result = await repository.GetByIdAsync(id, ct);

        if (result is null || result.UserId != userContext.UserId)
        {
            return ServiceResultFactory.Failure<RecurringTransactionResponse>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.RecurringTransaction.NotFound, ct));
        }

        return ServiceResultFactory.Success(
            MapToResponse(result),
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.RecurringTransaction.GetSuccess, ct));
    }

    public async Task<ServiceResult<PagedResponse<RecurringTransactionListItemResponse>>> GetListAsync(
        GetRecurringTransactionsRequest request,
        CancellationToken               ct = default)
    {
        var model = new GetRecurringTransactionsDbModel
        {
            UserId            = userContext.UserId,
            StatusId          = request.StatusId.HasValue ? (byte?)request.StatusId.Value : null,
            TransactionTypeId = request.TransactionTypeId.HasValue ? (byte?)request.TransactionTypeId.Value : null,
            IsSubscription    = false,
            PageNumber        = request.PageNumber,
            PageSize          = request.PageSize,
        };

        var db = await repository.GetListAsync(model, ct);

        var items = db.Items.Select(MapToListItem).ToList();

        var response = new PagedResponse<RecurringTransactionListItemResponse>(
            Items:      items,
            TotalCount: db.TotalCount,
            PageNumber: request.PageNumber,
            PageSize:   request.PageSize);

        return ServiceResultFactory.Success(
            response,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.RecurringTransaction.ListLoaded, ct));
    }

    public async Task<ServiceResult<RecurringTransactionResponse>> UpdateAsync(
        UpdateRecurringTransactionRequest request,
        CancellationToken                 ct = default)
    {
        var existing = await repository.GetByIdAsync(request.Id, ct);
        if (existing is null || existing.UserId != userContext.UserId)
        {
            return ServiceResultFactory.Failure<RecurringTransactionResponse>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.RecurringTransaction.NotFound, ct));
        }

        var endDate = request.EndDate is not null ? DateOnly.Parse(request.EndDate) : (DateOnly?)null;

        if (endDate.HasValue && endDate.Value <= existing.StartDate)
        {
            return ServiceResultFactory.Failure<RecurringTransactionResponse>(
                InternalResponseCodes.BadRequest,
                await messageProvider.GetMessagesAsync(MessageKeys.RecurringTransaction.EndDateBeforeStartDate, ct));
        }

        // Recompute NextGenerationDate from existing last-generated date
        var frequency = (RecurrenceFrequency)existing.FrequencyId;
        var nextDate  = existing.LastGeneratedDate.HasValue
            ? RecurringTransactionDateCalculator.ComputeNextGenerationDate(
                existing.LastGeneratedDate.Value, frequency,
                request.FrequencyInterval ?? existing.FrequencyInterval,
                existing.FrequencyUnit.HasValue ? (FrequencyUnit?)existing.FrequencyUnit.Value : null,
                request.DayOfMonth.HasValue  ? (byte?)request.DayOfMonth.Value  : existing.DayOfMonth,
                request.DayOfWeek.HasValue   ? (byte?)request.DayOfWeek.Value   : existing.DayOfWeek)
            : RecurringTransactionDateCalculator.ComputeFirstGenerationDate(
                existing.StartDate, frequency,
                request.DayOfMonth.HasValue ? (byte?)request.DayOfMonth.Value : existing.DayOfMonth,
                request.DayOfWeek.HasValue  ? (byte?)request.DayOfWeek.Value  : existing.DayOfWeek);

        var model = new UpdateRecurringTransactionDbModel
        {
            Id                = request.Id,
            UserId            = userContext.UserId,
            CategoryId        = request.CategoryId,
            Name              = request.Name.Trim(),
            Amount            = request.Amount,
            Description       = request.Description?.Trim(),
            FrequencyInterval = request.FrequencyInterval,
            FrequencyUnit     = request.FrequencyUnit.HasValue ? (byte?)request.FrequencyUnit.Value : null,
            DayOfMonth        = request.DayOfMonth.HasValue ? (byte?)request.DayOfMonth.Value : null,
            DayOfWeek         = request.DayOfWeek.HasValue  ? (byte?)request.DayOfWeek.Value  : null,
            EndDate           = endDate,
            Notes             = request.Notes?.Trim(),
            NextGenerationDate = nextDate,
        };

        await repository.UpdateAsync(model, ct);

        var updated = await repository.GetByIdAsync(request.Id, ct);

        await InvalidateForecastCacheAndEnqueueAsync(userContext.UserId, ct);

        return ServiceResultFactory.Success(
            MapToResponse(updated!),
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.RecurringTransaction.Updated, ct));
    }

    public async Task<ServiceResult<object?>> DeleteAsync(long id, CancellationToken ct = default)
    {
        var existing = await repository.GetByIdAsync(id, ct);
        if (existing is null || existing.UserId != userContext.UserId)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.RecurringTransaction.NotFound, ct));
        }

        await repository.DeleteAsync(id, userContext.UserId, ct);

        return ServiceResultFactory.Success<object?>(
            null,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.RecurringTransaction.Deleted, ct));
    }

    public async Task<ServiceResult<object?>> PauseAsync(long id, CancellationToken ct = default)
    {
        var existing = await repository.GetByIdAsync(id, ct);
        if (existing is null || existing.UserId != userContext.UserId)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.RecurringTransaction.NotFound, ct));
        }

        if (existing.StatusId == (byte)RecurrenceStatus.Paused)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.Conflict,
                await messageProvider.GetMessagesAsync(MessageKeys.RecurringTransaction.AlreadyPaused, ct));
        }

        await repository.PauseAsync(id, userContext.UserId, ct);

        return ServiceResultFactory.Success<object?>(
            null,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.RecurringTransaction.Paused, ct));
    }

    public async Task<ServiceResult<object?>> ResumeAsync(long id, CancellationToken ct = default)
    {
        var existing = await repository.GetByIdAsync(id, ct);
        if (existing is null || existing.UserId != userContext.UserId)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.RecurringTransaction.NotFound, ct));
        }

        if (existing.StatusId == (byte)RecurrenceStatus.Active)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.Conflict,
                await messageProvider.GetMessagesAsync(MessageKeys.RecurringTransaction.AlreadyActive, ct));
        }

        if (existing.StatusId == (byte)RecurrenceStatus.Expired)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.BadRequest,
                await messageProvider.GetMessagesAsync(MessageKeys.RecurringTransaction.CannotResumeExpired, ct));
        }

        await repository.ResumeAsync(id, userContext.UserId, ct);

        return ServiceResultFactory.Success<object?>(
            null,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.RecurringTransaction.Resumed, ct));
    }

    public async Task<ServiceResult<RecurringTransactionDashboardResponse>> GetDashboardAsync(
        CancellationToken ct = default)
    {
        var userId   = userContext.UserId;
        var summary  = await repository.GetDashboardSummaryAsync(userId, ct);
        var upcoming = await repository.GetUpcomingByUserAsync(userId, daysAhead: 7, ct);

        var upcomingPayments = upcoming
            .Where(u => !u.IsSubscription)
            .Select(u => new UpcomingPaymentDto(
                u.Id, u.Name, u.Amount, u.NextDate, u.DaysUntil,
                u.CategoryNameEn, u.CategoryNameAr))
            .ToList();

        var upcomingRenewals = upcoming
            .Where(u => u.IsSubscription)
            .Select(u => new UpcomingRenewalDto(
                u.Id, u.Name, u.Amount, u.NextDate, u.DaysUntil,
                u.ProviderName ?? string.Empty))
            .ToList();

        var response = new RecurringTransactionDashboardResponse
        {
            MonthlyRecurringIncome   = summary.MonthlyRecurringIncome,
            MonthlyRecurringExpenses = summary.MonthlyRecurringExpenses,
            ActiveDefinitionsCount   = summary.ActiveDefinitionsCount,
            ActiveSubscriptionsCount = summary.ActiveSubscriptionsCount,
            UpcomingPayments         = upcomingPayments,
            UpcomingRenewals         = upcomingRenewals,
        };

        return ServiceResultFactory.Success(
            response,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.RecurringTransaction.DashboardLoaded, ct));
    }

    // ── Subscriptions ──────────────────────────────────────────────────────────

    public async Task<ServiceResult<SubscriptionResponse>> CreateSubscriptionAsync(
        CreateSubscriptionRequest request,
        CancellationToken         ct = default)
    {
        var startDate = DateOnly.Parse(request.StartDate);
        var endDate   = request.EndDate is not null ? DateOnly.Parse(request.EndDate) : (DateOnly?)null;
        var frequency = (RecurrenceFrequency)request.FrequencyId;

        if (endDate.HasValue && endDate.Value <= startDate)
        {
            return ServiceResultFactory.Failure<SubscriptionResponse>(
                InternalResponseCodes.BadRequest,
                await messageProvider.GetMessagesAsync(MessageKeys.RecurringTransaction.EndDateBeforeStartDate, ct));
        }

        var nextDate = RecurringTransactionDateCalculator.ComputeFirstGenerationDate(
            startDate, frequency, null, null);

        var renewalDate   = request.RenewalDate is not null ? DateOnly.Parse(request.RenewalDate) : (DateOnly?)null;
        var trialEndDate  = request.TrialEndDate is not null ? DateOnly.Parse(request.TrialEndDate) : (DateOnly?)null;

        var model = new CreateRecurringTransactionDbModel
        {
            UserId             = userContext.UserId,
            CategoryId         = request.CategoryId,
            TransactionTypeId  = (byte)2, // Expense — subscriptions are always expenses
            Name               = request.Name.Trim(),
            Amount             = request.Amount,
            Description        = request.Description?.Trim(),
            FrequencyId        = (byte)request.FrequencyId,
            FrequencyInterval  = request.FrequencyInterval,
            FrequencyUnit      = request.FrequencyUnit.HasValue ? (byte?)request.FrequencyUnit.Value : null,
            DayOfMonth         = null,
            DayOfWeek          = null,
            StartDate          = startDate,
            EndDate            = endDate,
            IsSubscription     = true,
            Notes              = request.Notes?.Trim(),
            NextGenerationDate = renewalDate ?? nextDate,
            ProviderName       = request.ProviderName.Trim(),
            WebsiteUrl         = request.WebsiteUrl?.Trim(),
            AutoRenew          = request.AutoRenew,
            RenewalDate        = renewalDate,
            TrialEndDate       = trialEndDate,
        };

        var id = await repository.CreateAsync(model, ct);

        var created = await repository.GetByIdAsync(id, ct);

        return ServiceResultFactory.Success(
            MapToSubscriptionResponse(created!),
            InternalResponseCodes.Created,
            await messageProvider.GetMessagesAsync(MessageKeys.Subscription.Created, ct));
    }

    public async Task<ServiceResult<PagedResponse<SubscriptionListItemResponse>>> GetSubscriptionsAsync(
        GetSubscriptionsRequest request,
        CancellationToken       ct = default)
    {
        var model = new GetRecurringTransactionsDbModel
        {
            UserId         = userContext.UserId,
            StatusId       = request.StatusId.HasValue ? (byte?)request.StatusId.Value : null,
            IsSubscription = true,
            PageNumber     = request.PageNumber,
            PageSize       = request.PageSize,
        };

        var db = await repository.GetListAsync(model, ct);

        var items = db.Items.Select(MapToSubscriptionListItem).ToList();

        var response = new PagedResponse<SubscriptionListItemResponse>(
            Items:      items,
            TotalCount: db.TotalCount,
            PageNumber: request.PageNumber,
            PageSize:   request.PageSize);

        return ServiceResultFactory.Success(
            response,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Subscription.ListLoaded, ct));
    }

    public async Task<ServiceResult<SubscriptionResponse>> UpdateSubscriptionAsync(
        UpdateSubscriptionRequest request,
        CancellationToken         ct = default)
    {
        var existing = await repository.GetByIdAsync(request.Id, ct);
        if (existing is null || existing.UserId != userContext.UserId || !existing.IsSubscription)
        {
            return ServiceResultFactory.Failure<SubscriptionResponse>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Subscription.NotFound, ct));
        }

        var endDate    = request.EndDate is not null ? DateOnly.Parse(request.EndDate) : (DateOnly?)null;
        var renewalDate = request.RenewalDate is not null ? DateOnly.Parse(request.RenewalDate) : (DateOnly?)null;

        var model = new UpdateRecurringTransactionDbModel
        {
            Id                 = request.Id,
            UserId             = userContext.UserId,
            CategoryId         = request.CategoryId,
            Name               = request.Name.Trim(),
            Amount             = request.Amount,
            Description        = request.Description?.Trim(),
            EndDate            = endDate,
            Notes              = request.Notes?.Trim(),
            NextGenerationDate = renewalDate ?? existing.NextGenerationDate ?? existing.StartDate,
            ProviderName       = request.ProviderName.Trim(),
            WebsiteUrl         = request.WebsiteUrl?.Trim(),
            AutoRenew          = request.AutoRenew,
            RenewalDate        = renewalDate,
        };

        await repository.UpdateAsync(model, ct);

        var updated = await repository.GetByIdAsync(request.Id, ct);

        return ServiceResultFactory.Success(
            MapToSubscriptionResponse(updated!),
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Subscription.Updated, ct));
    }

    // ── Engine (IRecurringTransactionEngineService) ────────────────────────────

    public async Task ProcessDueTransactionsAsync(DateOnly upToDate, CancellationToken ct = default)
    {
        var dueDefinitions = await repository.GetDueAsync(upToDate, ct);

        foreach (var definition in dueDefinitions)
        {
            var frequency    = (RecurrenceFrequency)definition.FrequencyId;
            FrequencyUnit? unit = definition.FrequencyUnit.HasValue
                ? (FrequencyUnit)definition.FrequencyUnit.Value
                : null;

            var dates = RecurringTransactionDateCalculator.ComputeMissedDates(
                definition.LastGeneratedDate,
                definition.StartDate,
                upToDate,
                frequency,
                definition.FrequencyInterval,
                unit,
                definition.DayOfMonth,
                definition.DayOfWeek,
                definition.EndDate);

            foreach (var date in dates)
            {
                var nextDate = RecurringTransactionDateCalculator.ComputeNextGenerationDate(
                    date, frequency, definition.FrequencyInterval, unit,
                    definition.DayOfMonth, definition.DayOfWeek);

                var genResult = await repository.GenerateNextAsync(
                    new GenerateTransactionDbModel
                    {
                        DefinitionId       = definition.Id,
                        ForDate            = date,
                        NextGenerationDate = nextDate,
                    }, ct);

                if (!genResult.WasAlreadyDone)
                {
                    var templateCode = definition.IsSubscription
                        ? "RecurringSubscription.AutoRenewed"
                        : "RecurringPayment.Generated";

                    var parameters = definition.IsSubscription
                        ? new Dictionary<string, string>
                          {
                              ["ProviderName"] = definition.ProviderName ?? definition.Name,
                              ["Amount"]       = definition.Amount.ToString("N2"),
                          }
                        : new Dictionary<string, string>
                          {
                              ["Name"]   = definition.Name,
                              ["Amount"] = definition.Amount.ToString("N2"),
                              ["Date"]   = date.ToString("yyyy-MM-dd"),
                          };

                    await notificationPublisher.PublishAsync(
                        templateCode, definition.UserId, parameters, ct: ct);
                }
            }
        }
    }

    public async Task SendUpcomingNotificationsAsync(int daysAhead, CancellationToken ct = default)
    {
        var upcoming = await repository.GetUpcomingAsync(daysAhead, ct);

        foreach (var item in upcoming)
        {
            var templateCode = item.IsSubscription
                ? "RecurringSubscription.UpcomingRenewal"
                : "RecurringPayment.UpcomingDue";

            var parameters = item.IsSubscription
                ? new Dictionary<string, string>
                  {
                      ["ProviderName"] = item.ProviderName ?? item.Name,
                      ["Amount"]       = item.Amount.ToString("N2"),
                      ["RenewalDate"]  = item.NextDate.ToString("yyyy-MM-dd"),
                      ["DaysUntil"]    = item.DaysUntil.ToString(),
                  }
                : new Dictionary<string, string>
                  {
                      ["Name"]      = item.Name,
                      ["Amount"]    = item.Amount.ToString("N2"),
                      ["DueDate"]   = item.NextDate.ToString("yyyy-MM-dd"),
                      ["DaysUntil"] = item.DaysUntil.ToString(),
                  };

            await notificationPublisher.PublishAsync(
                templateCode, item.UserId, parameters, ct: ct);
        }
    }

    // ── Mapping helpers ────────────────────────────────────────────────────────

    private static string FrequencyLabel(byte frequencyId, int? interval, byte? unit)
    {
        return (RecurrenceFrequency)frequencyId switch
        {
            RecurrenceFrequency.Daily     => "Daily",
            RecurrenceFrequency.Weekly    => "Weekly",
            RecurrenceFrequency.Monthly   => "Monthly",
            RecurrenceFrequency.Quarterly => "Quarterly",
            RecurrenceFrequency.Yearly    => "Yearly",
            RecurrenceFrequency.Custom    => $"Every {interval} {((FrequencyUnit)(unit ?? 3)).ToString().TrimEnd('s')}(s)",
            _                             => "Unknown",
        };
    }

    private async Task InvalidateForecastCacheAndEnqueueAsync(long userId, CancellationToken ct)
    {
        await cacheService.RemoveAsync($"cashflow:forecast:{userId}:12");
        await cacheService.RemoveAsync($"cashflow:dashboard:{userId}");
        await backgroundJobService.EnqueueAsync(
            JobTypes.ComputeCashFlowForecast,
            new ComputeForecastPayload(userId),
            priority: 5,
            ct: ct);
    }

    private static RecurringTransactionResponse MapToResponse(RecurringTransactionDbResult r)
    {
        SubscriptionMetadataDto? sub = r.IsSubscription && r.ProviderName is not null
            ? new SubscriptionMetadataDto(r.ProviderName, r.WebsiteUrl, r.AutoRenew ?? true, r.RenewalDate, r.TrialEndDate)
            : null;

        return new RecurringTransactionResponse
        {
            Id                 = r.Id,
            CategoryId         = r.CategoryId,
            CategoryNameEn     = r.CategoryNameEn,
            CategoryNameAr     = r.CategoryNameAr,
            TransactionTypeId  = r.TransactionTypeId,
            Name               = r.Name,
            Amount             = r.Amount,
            Description        = r.Description,
            FrequencyId        = r.FrequencyId,
            FrequencyLabel     = FrequencyLabel(r.FrequencyId, r.FrequencyInterval, r.FrequencyUnit),
            FrequencyInterval  = r.FrequencyInterval,
            FrequencyUnit      = r.FrequencyUnit,
            DayOfMonth         = r.DayOfMonth,
            DayOfWeek          = r.DayOfWeek,
            StartDate          = r.StartDate,
            EndDate            = r.EndDate,
            StatusId           = r.StatusId,
            LastGeneratedDate  = r.LastGeneratedDate,
            NextGenerationDate = r.NextGenerationDate,
            Notes              = r.Notes,
            IsSubscription     = r.IsSubscription,
            Subscription       = sub,
            CreatedAt          = r.CreatedAt,
            UpdatedAt          = r.UpdatedAt,
        };
    }

    private static RecurringTransactionListItemResponse MapToListItem(RecurringTransactionDbResult r) =>
        new()
        {
            Id                 = r.Id,
            Name               = r.Name,
            Amount             = r.Amount,
            TransactionTypeId  = r.TransactionTypeId,
            FrequencyId        = r.FrequencyId,
            FrequencyLabel     = FrequencyLabel(r.FrequencyId, r.FrequencyInterval, r.FrequencyUnit),
            StatusId           = r.StatusId,
            NextGenerationDate = r.NextGenerationDate,
            CategoryNameEn     = r.CategoryNameEn,
            CategoryNameAr     = r.CategoryNameAr,
            IsSubscription     = r.IsSubscription,
            ProviderName       = r.ProviderName,
        };

    private static SubscriptionResponse MapToSubscriptionResponse(RecurringTransactionDbResult r) =>
        new()
        {
            Id                 = r.Id,
            Name               = r.Name,
            Amount             = r.Amount,
            Description        = r.Description,
            FrequencyId        = r.FrequencyId,
            FrequencyLabel     = FrequencyLabel(r.FrequencyId, r.FrequencyInterval, r.FrequencyUnit),
            StatusId           = r.StatusId,
            NextGenerationDate = r.NextGenerationDate,
            ProviderName       = r.ProviderName ?? string.Empty,
            WebsiteUrl         = r.WebsiteUrl,
            AutoRenew          = r.AutoRenew ?? true,
            RenewalDate        = r.RenewalDate,
            TrialEndDate       = r.TrialEndDate,
            CategoryNameEn     = r.CategoryNameEn,
            CategoryNameAr     = r.CategoryNameAr,
            Notes              = r.Notes,
            StartDate          = r.StartDate,
            EndDate            = r.EndDate,
            CreatedAt          = r.CreatedAt,
            UpdatedAt          = r.UpdatedAt,
        };

    private static SubscriptionListItemResponse MapToSubscriptionListItem(RecurringTransactionDbResult r) =>
        new()
        {
            Id            = r.Id,
            Name          = r.Name,
            Amount        = r.Amount,
            StatusId      = r.StatusId,
            RenewalDate   = r.RenewalDate,
            ProviderName  = r.ProviderName ?? string.Empty,
            WebsiteUrl    = r.WebsiteUrl,
            AutoRenew     = r.AutoRenew ?? true,
            FrequencyId   = r.FrequencyId,
            FrequencyLabel = FrequencyLabel(r.FrequencyId, r.FrequencyInterval, r.FrequencyUnit),
            CategoryNameEn = r.CategoryNameEn,
            CategoryNameAr = r.CategoryNameAr,
        };
}
