using Application.Features.Category.DbModels;

namespace Application.Interfaces.Repositories;

public interface ICategoryRepository
{
    Task<IReadOnlyList<CategoryDbResult>> GetListAsync(byte? typeId, CancellationToken ct = default);
}
