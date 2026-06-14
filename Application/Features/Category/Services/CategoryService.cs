using Application.Features.Category.DTOs;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Shared.Constants;
using Shared.Enums.System;
using Shared.Results;

namespace Application.Features.Category.Services;

internal sealed class CategoryService(
    ICategoryRepository categoryRepository,
    ICacheService       cacheService,
    IMessageProvider    messageProvider) : ICategoryService
{
    public async Task<ServiceResult<IReadOnlyList<CategoryResponse>>> GetListAsync(
        byte?             typeId,
        CancellationToken ct = default)
    {
        var cacheKey = $"categories:{typeId?.ToString() ?? "all"}";

        var cached = await cacheService.GetAsync<IReadOnlyList<CategoryResponse>>(cacheKey);
        if (cached is not null)
        {
            return ServiceResultFactory.Success(
                cached,
                InternalResponseCodes.OK,
                await messageProvider.GetMessagesAsync(MessageKeys.Category.LoadedSuccessfully, ct));
        }

        var db = await categoryRepository.GetListAsync(typeId, ct);

        var response = db
            .Select(c => new CategoryResponse(
                c.CategoryId,
                c.NameEn,
                c.NameAr,
                c.TransactionTypeId,
                null,   // icon URLs are not needed for dropdowns
                c.SortOrder))
            .ToList();

        await cacheService.SetAsync(cacheKey, (IReadOnlyList<CategoryResponse>)response);

        var message = await messageProvider.GetMessagesAsync(MessageKeys.Category.LoadedSuccessfully, ct);
        return ServiceResultFactory.Success<IReadOnlyList<CategoryResponse>>(response, InternalResponseCodes.OK, message);
    }
}
