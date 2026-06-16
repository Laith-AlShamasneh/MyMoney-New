using Application.Features.Category.DTOs;
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

        group.MapPost("get/list", async (
            GetCategoriesRequest request,
            ICategoryService     service,
            CancellationToken    ct) =>
        {
            byte? typeIdByte = request.TypeId.HasValue ? (byte?)request.TypeId.Value : null;
            var result = await service.GetListAsync(typeIdByte, ct);
            return result.ToHttpResponse();
        });
    }
}
