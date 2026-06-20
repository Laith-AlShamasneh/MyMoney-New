using Application.Common.Constants;
using Application.Features.CashFlow.Jobs;
using Application.Features.FinancialIntelligence.Jobs;
using Application.Features.Transaction.DbModels;
using Application.Features.Transaction.DTOs;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Shared.Constants;
using Shared.Enums.Finance;
using Shared.Enums.System;
using Shared.Results;

namespace Application.Features.Transaction.Services;

internal sealed class TransactionService(
    ITransactionRepository transactionRepository,
    IUserContext           userContext,
    IMessageProvider       messageProvider,
    ICacheService          cacheService,
    IBackgroundJobService  backgroundJobService) : ITransactionService
{
    public async Task<ServiceResult<TransactionSearchResponse>> SearchAsync(
        SearchTransactionsRequest request,
        CancellationToken         ct = default)
    {
        var model = new TransactionSearchDbModel
        {
            UserId     = userContext.UserId,
            TypeId     = request.TypeId.HasValue ? (byte?)request.TypeId.Value : null,
            CategoryId = request.CategoryId,
            DateFrom   = TryParseDate(request.DateFrom),
            DateTo     = TryParseDate(request.DateTo),
            AmountMin  = request.AmountMin,
            AmountMax  = request.AmountMax,
            Search     = request.Search?.Trim(),
            SortBy     = NormalizeSortBy(request.SortBy),
            SortDir    = NormalizeSortDir(request.SortDir),
            PageNumber = request.PageNumber,
            PageSize   = request.PageSize,
        };

        var db = await transactionRepository.SearchAsync(model, ct);

        var items = db.Items.Select(r => new TransactionItemDto(
            r.TransactionId,
            r.CategoryId,
            r.CategoryNameEn,
            r.CategoryNameAr,
            r.CategoryIcon,
            r.TransactionTypeId,
            r.Amount,
            r.Description,
            r.TransactionDate,
            r.Notes,
            r.CreatedAt)).ToList();

        var summary = new TransactionSummaryDto(
            TotalCount:    db.TotalCount,
            TotalIncome:   db.TotalIncome,
            TotalExpenses: db.TotalExpenses,
            NetAmount:     db.TotalIncome - db.TotalExpenses,
            AvgAmount:     db.AvgAmount,
            MaxAmount:     db.MaxAmount);

        var response = new TransactionSearchResponse(
            Summary:    summary,
            Items:      items,
            TotalCount: db.TotalCount,
            PageNumber: request.PageNumber,
            PageSize:   request.PageSize);

        var message = await messageProvider.GetMessagesAsync(MessageKeys.Transaction.SearchSuccess, ct);
        return ServiceResultFactory.Success(response, InternalResponseCodes.OK, message);
    }

    public async Task<ServiceResult<TransactionAnalyticsResponse>> GetAnalyticsAsync(
        GetAnalyticsRequest request,
        CancellationToken   ct = default)
    {
        var model = new TransactionAnalyticsDbModel
        {
            UserId   = userContext.UserId,
            DateFrom = TryParseDate(request.DateFrom),
            DateTo   = TryParseDate(request.DateTo),
        };

        var db = await transactionRepository.GetAnalyticsAsync(model, ct);

        var breakdown = db.CategoryBreakdown
            .Select(b => new CategoryBreakdownDto(
                b.CategoryId, b.NameEn, b.NameAr, b.TotalAmount, b.TxCount, b.Percentage))
            .ToList();

        var trend = db.MonthlyTrend
            .Select(t => new TrendPointDto(t.Year, t.Month, t.Income, t.Expenses))
            .ToList();

        var response = new TransactionAnalyticsResponse(
            CategoryBreakdown: breakdown,
            MonthlyTrend:      trend);

        var message = await messageProvider.GetMessagesAsync(MessageKeys.Transaction.AnalyticsLoaded, ct);
        return ServiceResultFactory.Success(response, InternalResponseCodes.OK, message);
    }

    public async Task<ServiceResult<TransactionDetailResponse>> GetByIdAsync(
        long              transactionId,
        CancellationToken ct = default)
    {
        var db = await transactionRepository.GetByIdAsync(userContext.UserId, transactionId, ct);

        if (db is null)
        {
            return ServiceResultFactory.Failure<TransactionDetailResponse>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Transaction.NotFound, ct));
        }

        var response = new TransactionDetailResponse(
            db.TransactionId,
            db.CategoryId,
            db.CategoryNameEn,
            db.CategoryNameAr,
            db.CategoryIcon,
            db.TransactionTypeId,
            db.Amount,
            db.Description,
            db.TransactionDate,
            db.Notes,
            db.CreatedAt,
            db.UpdatedAt);

        return ServiceResultFactory.Success(
            response,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Common.Success, ct));
    }

    public async Task<ServiceResult<CreateTransactionResponse>> CreateAsync(
        CreateTransactionRequest request,
        CancellationToken        ct = default)
    {
        var userId = userContext.UserId;
        var txDate = DateOnly.Parse(request.TransactionDate);

        var model = new CreateTransactionDbModel
        {
            UserId            = userId,
            CategoryId        = request.CategoryId,
            TransactionTypeId = (byte)request.TransactionTypeId,
            Amount            = request.Amount,
            Description       = request.Description?.Trim(),
            TransactionDate   = txDate,
            Notes             = request.Notes?.Trim(),
        };

        var newId = await transactionRepository.CreateAsync(model, ct);

        // 0 means the SP returned because the category/type combination was invalid
        if (newId == 0)
        {
            return ServiceResultFactory.Failure<CreateTransactionResponse>(
                InternalResponseCodes.BadRequest,
                await messageProvider.GetMessagesAsync(MessageKeys.Transaction.InvalidCategory, ct));
        }

        await InvalidateFILCacheAndEnqueueRecomputeAsync(userId, txDate.Year, txDate.Month, ct);

        var message = await messageProvider.GetMessagesAsync(MessageKeys.Transaction.Created, ct);
        return ServiceResultFactory.Success(
            new CreateTransactionResponse(newId),
            InternalResponseCodes.Created,
            message);
    }

    public async Task<ServiceResult<object?>> UpdateAsync(
        long                     transactionId,
        UpdateTransactionRequest request,
        CancellationToken        ct = default)
    {
        var userId = userContext.UserId;
        var newDate = DateOnly.Parse(request.TransactionDate);

        var model = new UpdateTransactionDbModel
        {
            UserId            = userId,
            TransactionId     = transactionId,
            CategoryId        = request.CategoryId,
            TransactionTypeId = (byte)request.TransactionTypeId,
            Amount            = request.Amount,
            Description       = request.Description?.Trim(),
            TransactionDate   = newDate,
            Notes             = request.Notes?.Trim(),
        };

        var result = await transactionRepository.UpdateAsync(model, ct);

        if (result.AffectedRows == 0)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Transaction.NotFound, ct));
        }

        // Recompute the new month unconditionally; recompute the old month only if the date crossed a month boundary.
        await InvalidateFILCacheAndEnqueueRecomputeAsync(userId, newDate.Year, newDate.Month, ct);

        var oldDate = result.OldTransactionDate;
        if (oldDate.Year != newDate.Year || oldDate.Month != newDate.Month)
            await backgroundJobService.EnqueueAsync(
                JobTypes.SnapshotRecompute,
                new SnapshotRecomputePayload(userId, oldDate.Year, oldDate.Month),
                priority: 3, ct: ct);

        var message = await messageProvider.GetMessagesAsync(MessageKeys.Transaction.Updated, ct);
        return ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK, message);
    }

    public async Task<ServiceResult<object?>> DeleteAsync(
        long              transactionId,
        CancellationToken ct = default)
    {
        var userId = userContext.UserId;

        var model = new DeleteTransactionDbModel
        {
            UserId        = userId,
            TransactionId = transactionId,
        };

        var result = await transactionRepository.DeleteAsync(model, ct);

        if (result.AffectedRows == 0)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Transaction.NotFound, ct));
        }

        await InvalidateFILCacheAndEnqueueRecomputeAsync(userId, result.DeletedDate.Year, result.DeletedDate.Month, ct);

        var message = await messageProvider.GetMessagesAsync(MessageKeys.Transaction.Deleted, ct);
        return ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK, message);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task InvalidateFILCacheAndEnqueueRecomputeAsync(
        long              userId,
        int               year,
        int               month,
        CancellationToken ct)
    {
        await cacheService.RemoveAsync($"fil:dashboard:{userId}");
        await backgroundJobService.EnqueueAsync(
            JobTypes.SnapshotRecompute,
            new SnapshotRecomputePayload(userId, year, month),
            priority: 3,
            ct: ct);

        await cacheService.RemoveAsync($"cashflow:forecast:{userId}:12");
        await cacheService.RemoveAsync($"cashflow:dashboard:{userId}");
        await backgroundJobService.EnqueueAsync(
            JobTypes.ComputeCashFlowForecast,
            new ComputeForecastPayload(userId),
            priority: 3,
            ct: ct);
    }

    private static DateOnly? TryParseDate(string? s) =>
        !string.IsNullOrEmpty(s) && DateOnly.TryParse(s, out var d) ? d : null;

    private static string NormalizeSortBy(string? sortBy) => sortBy switch
    {
        "Amount"          => "Amount",
        "Description"     => "Description",
        "CategoryName"    => "CategoryName",
        "CreatedAt"       => "CreatedAt",
        _                 => "TransactionDate",
    };

    private static string NormalizeSortDir(string? dir) =>
        string.Equals(dir, "ASC", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
}
