using Application.Interfaces.Services;
using WebApi.Common.Extensions;

namespace WebApi.Features.Category;

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("api/categories")
                       .WithTags("Categories")
                       .RequireAuthorization();

        group.MapGet("get/list", async (
            int?              typeId,
            ICategoryService  service,
            CancellationToken ct) =>
        {
            byte? typeIdByte = typeId.HasValue ? (byte?)typeId.Value : null;
            var result = await service.GetListAsync(typeIdByte, ct);
            return result.ToHttpResponse();
        });
    }
}
