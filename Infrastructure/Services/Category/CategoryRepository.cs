using Application.Features.Category.DbModels;
using Application.Interfaces.Database;
using Application.Interfaces.Repositories;
using Dapper;
using System.Data;

namespace Infrastructure.Services.Category;

internal sealed class CategoryRepository(IDbExecutor db) : ICategoryRepository
{
    public async Task<IReadOnlyList<CategoryDbResult>> GetListAsync(
        byte?             typeId,
        CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@TypeId", typeId, DbType.Byte);

        return await db.QueryListAsync<CategoryDbResult>(
            "MyMoney.usp_Category_GetList", p, ct);
    }
}
