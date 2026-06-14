using Application.Features.Category.DTOs;
using Shared.Results;

namespace Application.Interfaces.Services;

public interface ICategoryService
{
    Task<ServiceResult<IReadOnlyList<CategoryResponse>>> GetListAsync(byte? typeId, CancellationToken ct = default);
}
